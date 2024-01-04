using MIDIModificationFramework;
using MIDIModificationFramework.Generator;
using MIDIModificationFramework.MIDIEvents;
using Monad.FLParser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Note = MIDIModificationFramework.Note;

namespace flp2midi
{
    internal class Program
    {
        private static bool Color { get; set; }
        private static bool Echo { get; set; }
        private static bool Muted { get; set; }
        private static bool Extrakey { get; set; }
        private static bool Fullvelocity { get; set; }

        //TODO: Actually support feed correctly
        private static IEnumerable<Note> EchoNotes(IEnumerable<Note> notes, byte echoamount, uint feed, uint time, int ppq)
        {
            if (feed == 0)
            {
                return notes;
            }

            List<IEnumerable<Note>> echos = new List<IEnumerable<Note>>
            {
                notes
            };

            long echoed = 0;

            for (int i = 1; i < echoamount; i++)
            {
                notes = notes.OffsetTime((time * ppq) / 96.0 / 2);
                notes = notes.Select(note => new Note(note.Channel, note.Key, (byte)(note.Velocity / (12800.0 / feed / Math.Sqrt(Math.E))), note.Start, note.End));
                echoed += notes.Count();
                echos.Add(notes);
            }
            Console.WriteLine($"Echoed {echoed} notes");
            return echos.MergeAll();
        }

        private static void Main(string[] args)
        {
            Console.SetWindowSize(150, 40);
            Console.WriteLine("[2J[H[0m[1m[90m[96mflp2mid | ver1.4.1");
            GetArgs(args);

            string filePath = "";
            if (args.Length > 0)
            {
                filePath = args[0];
            }
            else
            {
                Console.WriteLine("[91mMissing filename[0m");
                ProcessStartInfo ps = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "& '" + Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Configure.ps1'"
                };
                Process powershellProcess = new Process { StartInfo = ps };
                powershellProcess.Start();
                return;
            }
            string tempFile = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".tmp");
            File.Delete(tempFile); //Delete old temp file if it exist
            ParallelStream streams = new ParallelStream(File.Open(tempFile, FileMode.Create));

            Console.WriteLine("[93mLoading FL Studio project file...");

            Project proj = Project.Load(filePath, false);

            string title = proj.ProjectTitle;
            string version = proj.VersionString;

            Console.WriteLine("[92mTitle: " + title + " | Version: " + version);

            if (!version.StartsWith("20"))
            {
                Console.WriteLine("[91mWARNING: Your project version is too old and might cause some weird issues during conversion, such as missing patterns and more. ");
                Console.WriteLine("Please open and save as your project in FL Studio 20 for better compatibility with this program. ");
            }

            object l = new object();

            Dictionary<int, Dictionary<Channel, Note[]>> patternDict = new Dictionary<int, Dictionary<Channel, Note[]>>();

            Parallel.ForEach(proj.Patterns, pat =>
            {
                int id = pat.Id;
                string name = pat.Name;

                Dictionary<Channel, Note[]> notes = pat.Notes.ToDictionary(c => c.Key, c =>
                {
                    byte channel = 0;
                    bool colorchan = false;

                    if (c.Key.Data is GeneratorData data && data.GeneratorName.ToLower() == "midi out")
                    {
                        if (data.PluginSettings[29] == 0x01)
                        {
                            colorchan = true;
                        }

                        channel = data.PluginSettings[4];
                    }

                    List<Note> noteList = new List<Note>(c.Value.Count);

                    double lastNoteZeroTick = -1.0;
                    foreach (Monad.FLParser.Note n in c.Value.OrderBy(n => n.Position))
                    {
                        Note newNote = new Note((colorchan || Color) ? n.Color : channel, Extrakey ? n.Key : Math.Min((byte)127, Math.Max((byte)0, n.Key)), (Fullvelocity ? (byte)255 : Math.Min((byte)127, Math.Max((byte)1, n.Velocity))), n.Position, n.Position + (double)n.Length);
                        noteList.Add(newNote);

                        if (lastNoteZeroTick != -1.0 && lastNoteZeroTick != newNote.Start)
                        {
                            lastNoteZeroTick = -1.0;
                            noteList[^2].End = newNote.Start;
                        }

                        if (newNote.Length == 0)
                        {
                            lastNoteZeroTick = newNote.Start;
                            newNote.End = double.PositiveInfinity;
                        }
                    }
                    return noteList.ToArray();
                });

                lock (l)
                {
                    patternDict.Add(id, notes);
                }

                Console.WriteLine($"[93mFound pattern {id} \"{name}\"");
            });

            int trackID = 0;
            Track[] tracks = proj.Tracks.Where(t => t.Items.Count != 0).ToArray();
            int trackCount = 0;

            ParallelFor(0, tracks.Length, Environment.ProcessorCount, new CancellationToken(false), i =>
            {

                Track track = tracks[i];

                Parallel.ForEach(track.Items, item =>
                {
                    if (item is PatternPlaylistItem && (item.Muted == false || Muted))
                    {
                        PatternPlaylistItem pi = item as PatternPlaylistItem;
                        Dictionary<Channel, Note[]> pattern = patternDict[pi.Pattern.Id];
                        Parallel.ForEach(pattern, c =>
                        {
                            IEnumerable<Note> shifted = c.Value
                                  .TrimStart(Math.Max(0, item.StartOffset))
                                  .TrimEnd(Math.Max(0, item.EndOffset == -1 ? item.Length : item.EndOffset))
                                  .OffsetTime(item.Position - item.StartOffset);

                            Channel channel = c.Key;

                            if (channel.Data is GeneratorData data)
                            {
                                if (data.EchoFeed > 0 && Echo)
                                {
                                    Console.WriteLine($"[93mDetected echo property with feed={data.EchoFeed}");
                                    shifted = EchoNotes(shifted, data.Echo, data.EchoFeed, data.EchoTime, proj.Ppq);
                                }
                            }
                            lock (l)
                            {
                                BufferedStream Stream = new BufferedStream(streams.GetStream(trackID++), 1 << 24);
                                MidiWriter TrackWriter = new MidiWriter(Stream);
                                TrackWriter.Write(shifted.ExtractEvents());
                                Console.WriteLine($"[93mGenerated MIDI track {trackID} out of FL track {(trackCount) + 1}/{tracks.Length}");
                                Stream.Close();
                            }
                        });
                    }
                });
                lock (l)
                {
                    trackCount++;
                }
            });

            streams.CloseAllStreams();

            MidiWriter writer = new MidiWriter(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid"));
            writer.Init((ushort)proj.Ppq);
            writer.InitTrack();
            writer.Write(new TempoEvent(0, (int)(60000000.0 / proj.Tempo)));
            writer.EndTrack();

            for (int i = 0; i < trackID; i++)
            {
                Console.WriteLine($"[93mWriting MIDI track {i + 1}/{trackID}");

                Stream stream = streams.GetStream(i, true);

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
            Console.WriteLine("[92mConversion completed! \n");
            File.Delete(tempFile);
            Console.WriteLine("[107m[97m-");
            Console.WriteLine("[31m ==================================================================== [[5m Notice [25m] ==================================================================== ");
            Console.WriteLine("[97m-");
            Console.WriteLine("[34m[22m This tool does not support exporting automations and MIDI CC. Only note events can be exported. ");
            Console.WriteLine(" If you used any of these, you must export them separately and merge them with either CJCAMM or SAFC or whatever. ");
            Console.WriteLine(" Below is a guide on how to export a midi file that contains only automation clips and MIDI CC in FL Studio 20.7: ");
            Console.WriteLine("[97m-[34m");
            Console.WriteLine(" 1. Select all clips in the playlist via Ctrl + A. ");
            Console.WriteLine(" 2. Mute all of them via Alt + M. ");
            Console.WriteLine(" 3. Navigate to 'Picker: Automation clips', should be located on the left side of the playlist. ");
            Console.WriteLine(" 4. Click the first item, then click the last item while holding Shift to select all items. ");
            Console.WriteLine(" 5. Right click on any item and choose 'Select in playlist'. ");
            Console.WriteLine(" 6. Unmute selected via Alt + Shift + M. ");
            Console.WriteLine("[97m-[34m");
            Console.WriteLine(" If you used only some automation and no MIDI CC, you're good to go up to this point. Just press Ctrl + Shift + M and export! ");
            Console.WriteLine(" MIDI CC is a lot more complicated to get around if you don't plan them ahead. ");
            Console.WriteLine(" It's strongly recommended that you create a separate MIDI Out in the channel rack only for MIDI CC while making your Black MIDI. ");
            Console.WriteLine(" Then all you have to do at this point is to put that MIDI Out channel on solo via Alt + Left Click on the green dot and export. ");
            Console.WriteLine(" If you unfortunately got MIDI CC messed up with note events already, things gets much more complicated but separating them is still possible. ");
            Console.WriteLine(" Just follow the steps below: ");
            Console.WriteLine("[97m-[34m");
            Console.WriteLine(" 1. Navigate to 'Browser: Current project', should be located on the left side of the window. ");
            Console.WriteLine(" 2. Right click on the folder 'Patterns' and choose 'Show only automation'. ");
            Console.WriteLine(" 3. Repeat the following operation on every single pattern listed in there: ");
            Console.WriteLine("     1. Select the pattern");
            Console.WriteLine("     2. Open channel rack");
            Console.WriteLine("     3. Right click and choose 'Cut' on every single MIDI Out in your channel rack. ");
            Console.WriteLine("     4. Navigate to 'Picker: Patterns'. ");
            Console.WriteLine("     5. The selected pattern should be automatically highlighted, click on it again in the browser if not. ");
            Console.WriteLine("     6. Right click on any item and choose 'Select in playlist'. ");
            Console.WriteLine("     7. Unmute selected via Alt + Shift + M. ");
            Console.WriteLine(" 4. Export MIDI. ");
            Console.WriteLine("[97m-[34m");
            Console.WriteLine("  Have fun making your amazing Black MIDI! ");
            Console.WriteLine("[97m-[0m");
            return;
        }

        //TODO: Abstract console vars
        private static void GetArgs(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-c":
                        {
                            Console.WriteLine("[95mEnforce mapping color to channel");
                            Color = true;
                            break;
                        }
                    case "-e":
                        {
                            Console.WriteLine("[95mEcho effect (beta)");
                            Echo = true;
                            break;
                        }
                    case "-m":
                        {
                            Console.WriteLine("[95mExport muted patterns");
                            Muted = true;
                            break;
                        }
                    case "-x":
                        {
                            Console.WriteLine("[95m132 keys");
                            Extrakey = true;
                            break;
                        }
                    case "-f":
                        {
                            Console.WriteLine("[95mFull velocity");
                            Fullvelocity = true;
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"[91mInvalid input: {args[i]}");
                            Console.WriteLine("Allowed arguments: -c -e -m -x -f");
                            break;
                        }
                }
            }
        }

        private static void ParallelFor(int from, int to, int threads, CancellationToken cancel, Action<int> func)
        {
            Dictionary<int, Task> tasks = new Dictionary<int, Task>();
            BlockingCollection<int> completed = new BlockingCollection<int>();

            void RunTask(int i)
            {
                Task t = new Task(() =>
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
                int t = completed.Take(cancel);
                tasks[t].Wait();
                tasks.Remove(t);
            }

            for (int i = from; i < to; i++)
            {
                RunTask(i);
                if (tasks.Count > threads)
                {
                    TryTake();
                }
            }

            while (completed.Count > 0 || tasks.Count > 0)
            {
                TryTake();
            }
        }
    }
}
