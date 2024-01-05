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
using System.Text;

namespace flp2midi
{
    class Program
    {
        static bool Color { get; set; }
        static bool Echo { get; set; }
        static bool Muted { get; set; }
        static bool Extrakey { get; set; }
        static bool Fullvelocity { get; set; }
        static IEnumerable<Note> EchoNotes(IEnumerable<Note> notes, byte echoamount, uint feed, uint time, int ppq)
        {
            if(feed == 0) return notes;
            List<IEnumerable<Note>> echos = new List<IEnumerable<Note>>();

            var echoed = 0;

            for(var i = 1; i < echoamount; i++)
            {
                notes = notes.OffsetTime((time * ppq) / 96.0 / 2);
                notes = notes.Select(note => new Note(note.Channel, note.Key, (byte)(note.Velocity / (12800.0 / feed / Math.Sqrt(Math.E))), note.Start, note.End));
                echoed += notes.Count();
                echos.Add(notes);
            }
            Console.WriteLine($"[93mEchoed {echoed} notes");
            return echos.MergeAll();
        }
        static void Main(string[] args)
        {
            Console.SetWindowSize(150, 40);
            Console.WriteLine("[2J[H[0m[1m[90m[96mflp2mid | ver1.4.1");
            Console.Write($"[95mParameters received:");
            for (var i = 0; i < args.Length; i++)
            {
                Console.Write($" {args[i]}");
            };Console.Write('\n');

            GetArgs(args);

            var filePath = "";
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

            Console.WriteLine("[93mLoading FL Studio project file...");

            Project proj = Project.Load(filePath, false);

            string title = proj.ProjectTitle;
            string version = proj.VersionString;

            Console.WriteLine("[92mTitle: " + title + " | Version: " + version);

            if(!version.StartsWith("20"))
            {
                Console.WriteLine("[91mWARNING: Your project version is too old and might cause some weird issues during conversion, such as missing patterns and more. ");
                Console.WriteLine("Please open and save as your project in FL Studio 20 for better compatibility with this program. ");
            }
            object l = new object();
            var patternDict = new Dictionary<int, Dictionary<Channel, Note[]>>();
            var channelList = new List<Channel>(proj.Channels);
            Parallel.ForEach(proj.Patterns, pat =>
            {
                int id = pat.Id;
                string name = pat.Name;

                var notes = new Dictionary<Channel, Note[]>();
                foreach (var c in channelList)
                {
                    byte channel = 0;
                    var colorchan = false;

                    if (c.Data is GeneratorData data && data.GeneratorName.ToLower() == "midi out")
                    {
                        if (data.PluginSettings[29] == 0x01) colorchan = true;
                        channel = data.PluginSettings[4];
                    }
                    var noteList = new List<Note>();
                    if (pat.Notes.ContainsKey(c))
                    {
                        var lastNoteZeroTick = -1.0;
                        foreach (var n in pat.Notes[c].OrderBy(n => n.Position))
                        {
                            var newNote = new Note((colorchan || Color) ? n.Color : channel, Extrakey ? n.Key : Math.Min((byte)127, Math.Max((byte)0, n.Key)), Fullvelocity ? (byte)255 : Math.Min((byte)127, Math.Max((byte)1, n.Velocity)), (double)n.Position, (double)n.Position + (double)n.Length);
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
                    }
                    notes.Add(c, noteList.ToArray());
                }
                lock (l)
                {
                    patternDict.Add(id, notes);
                }

                Console.WriteLine($"[93mFound pattern {id} \"{name}\"");
            });
            var tracks = proj.Tracks.ToArray();
            var TrackCount = proj.Tracks.Where(t => t.Items.Count != 0).Count();
            var FLtrack = 0;
            var writer = new MidiWriter(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid"));
            writer.Init((ushort)proj.Ppq);
            writer.InitTrack();
            writer.Write(new TempoEvent(0, (int)(60000000.0 / proj.Tempo)));
            writer.EndTrack();
            for(var i = 0; i < TrackCount; i++)
            {
                var track = tracks[i];
                var firstloop = true;
                var fltrk = new List<IEnumerable<MIDIEvent>>();
                foreach (var item in track.Items)
                {   
                    var pointer = 0;
                    if (item is PatternPlaylistItem && (item.Muted == false || Muted))
                    {
                        var pi = item as PatternPlaylistItem;
                        var pattern = patternDict[pi.Pattern.Id];
                        foreach (var c in pattern)
                        {
                            var Events = new List<MIDIEvent>();
                            if (firstloop) Events.Add(new TextEvent(0, 0x03, Encoding.ASCII.GetBytes($"MIDI Out #{pointer + FLtrack * proj.Channels.Count + 1} @{FLtrack + 1}")));
                            var shifted = c.Value.TrimStart(Math.Max(0, item.StartOffset)).TrimEnd(Math.Max(0, item.EndOffset == -1 ? item.Length : item.EndOffset)).OffsetTime(item.Position - item.StartOffset);
                            var channel = c.Key;
                            if (channel.Data is GeneratorData data)
                            {
                                if (data.EchoFeed > 0 && Echo)
                                {
                                    shifted = EchoNotes(shifted, data.Echo, data.EchoFeed, data.EchoTime, proj.Ppq);
                                }
                            }
                            Console.WriteLine($"[93mGenerated MIDI track {pointer + FLtrack * proj.Channels.Count + 1} out of FL track {FLtrack + 1}/{TrackCount}");
                            if (firstloop)
                            {
                                Events.AddRange(shifted.ExtractEvents());
                                fltrk.Add(Events);
                            } else
                            {
                                var merger = new List<IEnumerable<MIDIEvent>>();
                                merger.Add(fltrk[pointer]);
                                merger.Add(shifted.ExtractEvents());
                                fltrk[pointer] = merger.MergeAllTracks();
                            }
                                
                            pointer++;
                        }
                        firstloop = false;
                    }
                }
                var written = 1;
                foreach (var midtrk in fltrk)
                {
                    Console.WriteLine($"[93mWritten {written++} tracks");
                    writer.WriteTrack(midtrk);
                }
                FLtrack++;
            }
            writer.Close();
            Console.WriteLine("[92mConversion completed! \n");
            Console.WriteLine("[107m[97m-[31m\n ==================================================================== [[5m Notice [25m] ==================================================================== ");
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
        static void GetArgs(string[] args)
        {
            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-c":
                        {
                            Color = true;
                            break;
                        }
                    case "-e":
                        {
                            Echo = true;
                            break;
                        }
                    case "-m":
                        {
                            Muted = true;
                            break;
                        }
                    case "-x":
                        {
                            Extrakey = true;
                            break;
                        }
                    case "-f":
                        {
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
    }
}
