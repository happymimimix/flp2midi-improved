using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Monad.FLParser;
using MIDIModificationFramework;
using MIDIModificationFramework.Generator;
using MIDIModificationFramework.MIDIEvents;
using Note = MIDIModificationFramework.Note;
using System.Collections.Concurrent;

namespace flp2midi
{
  class Program
  {
    static bool Color { get; set; }
    static bool Echo { get; set; }
    static bool Muted { get; set; }
    static bool Extrakey { get; set; }
    static bool Fullvelocity { get; set; }

    //TODO: Actually support feed correctly
    static IEnumerable<Note> EchoNotes(IEnumerable<Note> notes, byte echoamount, uint feed, uint time, int ppq)
    {
      if(feed == 0) return notes;

      List<IEnumerable<Note>> echos = new List<IEnumerable<Note>>();

      echos.Add(notes);

      long echoed = 0;

      for(var i = 1; i < echoamount; i++)
      {
        notes = notes.OffsetTime((time * ppq) / 96.0 / 2);
        notes = notes.Select(note => new Note(note.Channel, note.Key, (byte)(note.Velocity / (12800.0 / feed / Math.Sqrt(Math.E))), note.Start, note.End));
        echoed+= notes.Count();
        echos.Add(notes);
      }
      Console.WriteLine($"Echoed {echoed} notes");
      return echos.MergeAll();
    }

    static void Main(string[] args)
    {

      GetArgs(args);
      Console.WriteLine("flp2midi | Version: 1.4.1");

      var filePath = "";
      if (args.Length > 0)
      {
        filePath = args[0];
      } else {
        Console.WriteLine("Missing filename");
        return;
      }
      var tempFile = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid.tmp");
      File.Delete(tempFile); //Delete old temp file if it exist
      var streams = new ParallelStream(File.Open(tempFile, FileMode.Create));

      Console.WriteLine("Loading FL Studio project file...");

      Project proj = Project.Load(filePath, false);

      string title = proj.ProjectTitle;
      string version = proj.VersionString;

      Console.WriteLine("Title: " + title + " | Version: " + version);

      if(!version.StartsWith("20"))
      {
        Console.WriteLine("Your FL Studio version is too old! Consider load and saveas your project in FL studio 20 or above. ");
      }

      object l = new object();

      var patternDict = new Dictionary<int, Dictionary<Channel, Note[]>>();

      Parallel.ForEach(proj.Patterns, pat =>
      {
        int id = pat.Id;
        string name = pat.Name;

        var notes = pat.Notes.ToDictionary(c => c.Key, c =>
        {
          byte channel = 0;
          var colorchan = false;

          if(c.Key.Data is GeneratorData data && data.GeneratorName.ToLower() == "midi out")
          {
            if(data.PluginSettings[29] == 0x01) colorchan = true;
            channel = data.PluginSettings[4];
          }

          var noteList = new List<Note>(c.Value.Count);

          var lastNoteZeroTick = -1.0;
          foreach(var n in c.Value.OrderBy(n => n.Position))
          {
            var newNote = new Note((colorchan || Color) ? n.Color : channel, Extrakey ? n.Key : Math.Min((byte)127, n.Key), (byte)(Fullvelocity ? 128 : n.Velocity), (double)n.Position, (double)n.Position + (double)n.Length);
            noteList.Add(newNote);

            if(lastNoteZeroTick != -1.0 && lastNoteZeroTick != newNote.Start)
            {
              lastNoteZeroTick = -1.0;
              noteList[^2].End = newNote.Start;
            }

            if(newNote.Length == 0)
            {
              lastNoteZeroTick = newNote.Start;
              newNote.End = double.PositiveInfinity;
            }
          }
          return noteList.ToArray();
        });

        lock(l)
        {
          patternDict.Add(id, notes);
        }

        Console.WriteLine($"Found pattern {id} \"{name}\"");
      });

      var trackID = 0;
      var tracks = proj.Tracks.Where(t => t.Items.Count != 0).ToArray();
      var trackCount = 0;

      ParallelFor(0, tracks.Length, Environment.ProcessorCount, new CancellationToken(false), i =>
      {

        var track = tracks[i];

        Parallel.ForEach(track.Items, item =>
        {
          if(item is PatternPlaylistItem && (item.Muted == false || Muted))
          {
            var pi = item as PatternPlaylistItem;
            var pattern = patternDict[pi.Pattern.Id];
            Parallel.ForEach(pattern, c =>
            {
              var shifted = c.Value
                            .TrimStart(Math.Max(0, item.StartOffset))
                            .TrimEnd(Math.Max(0, item.EndOffset == -1 ? item.Length : item.EndOffset))
                            .OffsetTime(item.Position - item.StartOffset);

              var channel = c.Key;

              if(channel.Data is GeneratorData data)
              {
                if(data.EchoFeed > 0 && Echo)
                {
                  Console.WriteLine($"Detected echo property with feed={data.EchoFeed}");
                  shifted = EchoNotes(shifted, data.Echo, data.EchoFeed, data.EchoTime, proj.Ppq);
                }
              }
              lock(l)
              {
                var Stream = new BufferedStream(streams.GetStream(trackID++), 1 << 24);
                var TrackWriter = new MidiWriter(Stream);
                TrackWriter.Write(shifted.ExtractEvents());
              Console.WriteLine($"Generated MIDI track {trackID} out of FL track {(trackCount) + 1}/{tracks.Length}");
              Stream.Close();
              }
            });
          }
        });
        lock(l)
        {
          trackCount++;
        }
      });

      streams.CloseAllStreams();

      var writer = new MidiWriter(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid"));
      writer.Init((ushort)proj.Ppq);
      writer.InitTrack();
      writer.Write(new TempoEvent(0, (int)(60000000.0 / proj.Tempo)));
      writer.EndTrack();

      for(int i = 0; i < trackID; i++)
      {
        Console.WriteLine($"Writing MIDI track {i + 1}/{trackID}");

        var stream = streams.GetStream(i, true);

        stream.Position = 0;
        unchecked
        {
          writer.WriteTrack(stream);
        }
        stream.Close();
      }

      writer.Close();
      streams.CloseAllStreams();
      streams.Dispose();
      File.Delete(tempFile);
      return;
    }

    //TODO: Abstract console vars
    static void GetArgs(string[] args)
    {
      for(var i = 1; i < args.Length; i++)
      {
        switch(args[i])
        {
          case "-c":
          {
            Console.WriteLine("Force color mapping");
            Color = true;
            break;
          }
          case "-e":
          {
            Console.WriteLine("Echo effect (beta)");
            Echo = true;
            break;
          }
          case "-m":
          {
            Console.WriteLine("Export muted patterns");
            Muted = true;
            break;
          }
          case "-x":
          {
            Console.WriteLine("131 keys");
            Extrakey = true;
            break;
          }
          case "-f":
          {
            Console.WriteLine("Full velocity");
            Fullvelocity = true;
            break;
          }
          default:
          {
            Console.WriteLine($"Invalid input: {args[i]}");
            Console.WriteLine("Allowed arguments: -c -e -m -x -f");
            break;
          }
        }
      }
    }

    static void ParallelFor(int from, int to, int threads, CancellationToken cancel, Action<int> func)
    {
      Dictionary<int, Task> tasks = new Dictionary<int, Task>();
      BlockingCollection<int> completed = new BlockingCollection<int>();

      void RunTask(int i)
      {
        var t = new Task(() =>
        {
          try
          {
            func(i);
          }
          finally
          {
            completed.Add(i);
          }
        });
        tasks.Add(i, t);
        t.Start();
      }

      void TryTake()
      {
        var t = completed.Take(cancel);
        tasks[t].Wait();
        tasks.Remove(t);
      }

      for(int i = from; i < to; i++)
      {
        RunTask(i);
        if(tasks.Count > threads) TryTake();
      }

      while(completed.Count > 0 || tasks.Count > 0) TryTake();
    }
  }
}
