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
using System.Runtime.InteropServices;
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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        static IEnumerable<Note> EchoNotes(IEnumerable<Note> notes, byte echoamount, uint feed, uint time, int ppq)
        {
            unsafe
            {
                if (feed == 0) return notes;
                List<IEnumerable<Note>> echos = new List<IEnumerable<Note>>();

                var echoed = 0;

                for (byte i = 1; i < echoamount; i++)
                {
                    notes = notes.OffsetTime((time * ppq) / 96.0 / 2);
                    notes = notes.Select(note => new Note(note.Channel, note.Key, (byte)(note.Velocity / (12800.0 / feed / Math.Sqrt(Math.E))), note.Start, note.End));
                    echoed += notes.Count();
                    echos.Add(notes);
                }
                Console.WriteLine($"[93mEchoed {echoed} notes");
                return echos.MergeAll(); 
            }
        }

        private static void Main(string[] args)
        {
            unsafe
            {
                IntPtr hOut = GetStdHandle(STD_OUTPUT_HANDLE);
                GetConsoleMode(hOut, out uint dwMode);
                dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                SetConsoleMode(hOut, dwMode);

                Console.SetWindowSize(150, 40);
                Console.WriteLine("[2J[H[0m[1m[90m[96m >  flp2midi | ver1.6.3");
                IntPtr handle = GetConsoleWindow();

                ShowWindow(handle, SW_SHOW);

                if (args.Length >= 1)
                {
                    Console.Write("[95mParameters received: [");
                    for (int i = 0; i < args.Length - 1; i++)
                    {
                        Console.Write($"\"{args[i]}\", ");
                    };
                    Console.WriteLine($"\"{args[args.Length - 1]}\"]");
                }

                GetArgs(args);

                string filePath = "";
                if (args.Length >= 1)
                {
                    filePath = args[0];
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"[91mFile not found: {filePath}[0m");
                        Thread.Sleep(3000);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("[91mMissing filename[0m");
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "Powershell.EXE",
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    using (StreamWriter sw = process.StandardInput)
                    {
                        sw.WriteLine("Set-Location $PSScriptRoot;Add-Type -AssemblyName System.Windows.Forms;$browsefiles = New-Object System.Windows.Forms.OpenFileDialog;$browsefiles.Filter = \"FL Studio Project Files (*.flp)|*.flp\";$browsefiles.Title = \"Please Select Your FL Studio Project File...\";if ($browsefiles.ShowDialog() -eq 'OK'){;$form = New-Object System.Windows.Forms.Form;$form.Text = \"flp2midi v1.6.3\";$form.Size = New-Object System.Drawing.Size(300,320);$form.StartPosition = \"CenterScreen\";$form.FormBorderStyle = \"FixedToolWindow\";$form.TopMost = $true;$title = New-Object System.Windows.Forms.Label;$title.Font = \"dosis,18\";$title.Text = \"flp2midi v1.6.3\";$title.TextAlign = \"MiddleCenter\";$title.Size = \"300,35\";$title.Location = New-Object System.Drawing.Point(0,20);$text = New-Object System.Windows.Forms.Label;$text.Font = \"dosis,12\";$text.Text = \"options: \";$text.TextAlign = \"MiddleLeft\";$text.Size = \"70,20\";$text.Location = New-Object System.Drawing.Point(15,70);$checkbox1 = New-Object System.Windows.Forms.CheckBox;$checkbox1.Text = \"Enforce mapping color to channel\";$checkbox1.Font = \"dosis,10\";$checkbox1.TextAlign = \"MiddleLeft\";$checkbox1.Size = \"250,20\";$checkbox1.Location = New-Object System.Drawing.Point(15,100);$checkbox2 = New-Object System.Windows.Forms.CheckBox;$checkbox2.Text = \"Echo effect (beta)\";$checkbox2.Font = \"dosis,10\";$checkbox2.TextAlign = \"MiddleLeft\";$checkbox2.Size = \"250,20\";$checkbox2.Location = New-Object System.Drawing.Point(15,130);$checkbox3 = New-Object System.Windows.Forms.CheckBox;$checkbox3.Text = \"Export muted patterns\";$checkbox3.Font = \"dosis,10\";$checkbox3.TextAlign = \"MiddleLeft\";$checkbox3.Size = \"250,20\";$checkbox3.Location = New-Object System.Drawing.Point(15,160);$checkbox4 = New-Object System.Windows.Forms.CheckBox;$checkbox4.Text = \"132 keys\";$checkbox4.Font = \"dosis,10\";$checkbox4.TextAlign = \"MiddleLeft\";$checkbox4.Size = \"250,20\";$checkbox4.Location = New-Object System.Drawing.Point(15,190);$checkbox5 = New-Object System.Windows.Forms.CheckBox;$checkbox5.Text = \"Full velocity\";$checkbox5.Font = \"dosis,10\";$checkbox5.TextAlign = \"MiddleLeft\";$checkbox5.Size = \"250,20\";$checkbox5.Location = New-Object System.Drawing.Point(15,220);$button = New-Object System.Windows.Forms.Button;$button.Text = \"Execute\";$button.Font = \"dosis,12\";$button.TextAlign = \"MiddleCenter\";$button.Location = New-Object System.Drawing.Point(200,250);$button.Add_Click({;$arguments = @();$path = $browsefiles.FileName;$arguments += \"`\"$path`\"\";if ($checkbox1.Checked) { $arguments += \" -c\" };if ($checkbox2.Checked) { $arguments += \" -e\" };if ($checkbox3.Checked) { $arguments += \" -m\" };if ($checkbox4.Checked) { $arguments += \" -x\" };if ($checkbox5.Checked) { $arguments += \" -f\" };$form.Close();Start-Process -FilePath \"" + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "\" -ArgumentList $arguments -Wait;});$form.Controls.Add($title);$form.Controls.Add($text);$form.Controls.Add($checkbox1);$form.Controls.Add($checkbox2);$form.Controls.Add($checkbox3);$form.Controls.Add($checkbox4);$form.Controls.Add($checkbox5);$form.Controls.Add($button);[Windows.Forms.Application]::Run($form);}");
                    }
                    Thread.Sleep(3000);
                    ShowWindow(handle, SW_HIDE);
                    process.WaitForExit();
                    ShowWindow(handle, SW_SHOW);
                    return;
                }
                string tempFile = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".tmp");
                File.Delete(tempFile);
                ParallelStream streams = new ParallelStream(File.Open(tempFile, FileMode.Create));
                Console.WriteLine("[93mLoading FL Studio project file...");
                Project FLPROJ = Project.Load(filePath, false);
                Console.WriteLine("[92mTitle: " + FLPROJ.ProjectTitle + " | Version: " + FLPROJ.VersionString);
                if (!FLPROJ.VersionString.StartsWith("20.7"))
                {
                    Console.WriteLine("[91mWARNING: Your project version is too old and might cause some weird issues during conversion, such as missing patterns and more. ");
                    Console.WriteLine("Please open and save as your project in FL Studio 20.7 for better compatibility with this program. ");
                    Console.WriteLine("Press return to continue anyways...");
                    Console.ReadLine();
                }
                List<Pattern> PATTERNS = FLPROJ.Patterns;
                List<Channel> CHANNELS = new List<Channel>(FLPROJ.Channels);
                int CHANNEL_COUNT = FLPROJ.Channels.Count;
                Track[] TRACKS = FLPROJ.Tracks.Where(t => t.Items.Count != 0).ToArray();
                int TRACK_COUNT = TRACKS.Length;
                ushort PPQ = FLPROJ.Ppq;
                MidiWriter writer = new MidiWriter(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(filePath) + ".mid"));
                Console.WriteLine($"[92mProject PPQ: {PPQ}");
                Console.WriteLine($"[92mProject BPM: {FLPROJ.Tempo}");
                writer.Init((ushort)PPQ);
                writer.InitTrack();
                writer.Write(new TempoEvent(0, (int)(60000000.0/FLPROJ.Tempo)));
                writer.Write(new TextEvent(0, (byte)(TextEventType.TrackName), System.Text.Encoding.UTF8.GetBytes("Conductor")));
                writer.EndTrack();
                FLPROJ = null; //Unload unnecessary parts of the project
                Console.WriteLine("[95mCollecting garbage..."); GC.Collect(); GC.WaitForPendingFinalizers(); Console.WriteLine("[95mGarbage collected! ");

                ConcurrentDictionary<int, Dictionary<Channel, Note[]>> patternDict = new ConcurrentDictionary<int, Dictionary<Channel, Note[]>>();
                Parallel.ForEach(PATTERNS, pat =>
                {
                    int id = pat.Id;
                    string name = pat.Name;

                    Dictionary<Channel, Note[]> notes = new Dictionary<Channel, Note[]>();
                    foreach (Channel c in CHANNELS)
                    {
                        byte channel = 0;
                        bool colorchan = true;

                        if (c.Data is GeneratorData data && data.GeneratorName.ToLower() == "midi out")
                        {
                            if (data.PluginSettings[29] == 0)
                            {
                                colorchan = false;
                            }

                            channel = data.PluginSettings[4];
                        }

                        List<Note> noteList = new List<Note>();
                        if (pat.Notes.ContainsKey(c))
                        {
                            double lastNoteZeroTick = -1.0;
                            foreach (Monad.FLParser.Note n in pat.Notes[c].OrderBy(n => n.Position))
                            {
                                Note newNote = new Note((colorchan || Color) ? n.Color : channel, Extrakey ? n.Key : Math.Min((byte)127, Math.Max((byte)0, n.Key)), Fullvelocity ? (byte)127 : Math.Min((byte)127, Math.Max((byte)1, n.Velocity)), n.Position, n.Position + (double)n.Length);
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
                    patternDict.TryAdd(id, notes);
                    Console.WriteLine($"[93mFound pattern {id} \"{name}\"");
                });
                PATTERNS = null; // Free memory
                CHANNELS = null;
                Console.WriteLine("[95mCollecting garbage..."); GC.Collect(); GC.WaitForPendingFinalizers(); Console.WriteLine("[95mGarbage collected! ");
                ushort Processed = 0;
                ushort Converted = 0;
                ushort Buffered = 0;
                object locker = new object();
                ParallelFor(0, TRACK_COUNT, Environment.ProcessorCount, new CancellationToken(false), i =>
                {
                    Track track = TRACKS[i];
                    bool firstloop = true;
                    BufferedStream stream = new BufferedStream(streams.GetStream(i), 1 << 29);
                    List<IEnumerable<Note>> fltrk = new List<IEnumerable<Note>>();
                    MemoryStream[] tempstreams = new MemoryStream[CHANNEL_COUNT];
                    for (int idx = 0; idx < tempstreams.Length; idx++)
                    {
                        tempstreams[idx] = new MemoryStream();
                    }
                    foreach (IPlaylistItem item in track.Items)
                    {
                        ushort channelID = 0;
                        if (item is PatternPlaylistItem && (item.Muted == false || Muted))
                        {
                            PatternPlaylistItem pi = item as PatternPlaylistItem;
                            Dictionary<Channel, Note[]> pattern = patternDict[pi.Pattern.Id];
                            foreach (KeyValuePair<Channel, Note[]> c in pattern)
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
                                        shifted = EchoNotes(shifted, data.Echo, data.EchoFeed, data.EchoTime, PPQ);
                                    }
                                }
                                if (firstloop)
                                {
                                    fltrk.Add(shifted);
                                }
                                else
                                {
                                    fltrk[channelID] = fltrk[channelID].MergeWith(shifted);
                                }
                                lock (locker)
                                {
                                    Console.WriteLine($"[93mProcessed MIDI track {++channelID} in FL playlist track {Processed + 1}/{TRACK_COUNT}");
                                }
                            }
                        }
                        else
                        {
                            if (firstloop)
                            {
                                for (int j = 0; j < CHANNEL_COUNT; j++)
                                {
                                    fltrk.Add(new List<Note>());
                                }
                                lock (locker)
                                {
                                    Console.WriteLine($"[93mProcessed MIDI track {++channelID} in FL playlist track {Processed + 1}/{TRACK_COUNT}");
                                }
                            }
                        }
                        firstloop = false;
                    }
                    lock (locker) { Processed++; }
                    ushort ConvertedCH = 0;
                    object locker2 = new object();
                    ParallelFor(0, CHANNEL_COUNT, Environment.ProcessorCount, new CancellationToken(false), pointer =>
                    {
                        unchecked
                        {
                        MemoryStream tempstream = tempstreams[pointer];
                        BufferedStream bufferedstream = new BufferedStream(tempstream, 1 << 29);
                        MidiWriter trackWriter = new MidiWriter(bufferedstream);
                        trackWriter.Write(fltrk[pointer].ExtractEvents());
                        fltrk[pointer] = null;
                        bufferedstream.Close();
                        bufferedstream = null;
                        tempstream.Close();
                        tempstream = null;
                            lock (locker2)
                            {
                                Console.WriteLine($"[93mConverted track {++ConvertedCH}/{CHANNEL_COUNT} in FL playlist track {Converted + 1}/{TRACK_COUNT}");
                            }
                        }
                    });
                    lock (locker) { Converted++; }
                    ushort BufferedCH = 0;
                    for (int pointer = 0; pointer < CHANNEL_COUNT; pointer++)
                    {
                        unchecked
                        {
                        MemoryStream tempstream = tempstreams[pointer];
                        byte[] buffer = tempstream.ToArray();
                        BinaryWriter binaryWriter = new BinaryWriter(stream);
                        binaryWriter.Write(buffer.Length);
                        binaryWriter.Write(buffer);
                        buffer = null;
                        binaryWriter = null;
                            lock (locker2)
                            {
                                Console.WriteLine($"[93mBuffered track {++BufferedCH}/{CHANNEL_COUNT} in FL playlist track {Buffered + 1}/{TRACK_COUNT}");
                            }
                        }
                    }
                    lock (locker) { Buffered++; }
                    fltrk = null;
                    stream.Close();
                });
                TRACKS = null;
                patternDict = null;
                Console.WriteLine("[95mCollecting garbage..."); GC.Collect(); GC.WaitForPendingFinalizers(); Console.WriteLine("[95mGarbage collected! ");
                streams.CloseAllStreams();
                for (int i = 0; i < TRACK_COUNT; i++)
                {
                    Stream stream = streams.GetStream(i, true);
                    MemoryStream[] tempstreams = new MemoryStream[CHANNEL_COUNT];
                    BinaryReader binaryReader = new BinaryReader(stream);

                    stream.Position = 0;
                    for (int j = 0; j < CHANNEL_COUNT; j++)
                    {
                        int length = binaryReader.ReadInt32();
                        byte[] buffer = binaryReader.ReadBytes(length);
                        tempstreams[j] = new MemoryStream(buffer);
                        Console.WriteLine($"[93mLoaded {j + 1} tracks from buffer");
                    }
                    for (int j = 0; j < CHANNEL_COUNT; j++)
                    {
                        tempstreams[j].Position = 0;
                        unchecked
                        {
                            writer.WriteTrack(tempstreams[j]);
                        }
                        tempstreams[j].Close();
                        Console.WriteLine($"[93mWritten {j + i * CHANNEL_COUNT + 1} tracks");
                    }
                }
                writer.Close();
                streams.CloseAllStreams();
                streams.Dispose();
                Console.WriteLine("[95mCollecting garbage..."); GC.Collect(); GC.WaitForPendingFinalizers();
                Console.WriteLine("[95mGarbage collected! ");
                File.Delete(tempFile);
                Console.WriteLine("[92mConversion completed! ");
                Console.WriteLine("[107m[97m\n[31m");
                Console.WriteLine(" ==================================================================== [[5m Notice [25m] ==================================================================== ");
                Console.WriteLine("[97m-[34m");
                Console.WriteLine("[22m This tool does not support exporting automations and MIDI CC. Only note events can be exported. ");
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
                Console.WriteLine("  Press return to quit, have fun making your amazing Black MIDI! ");
                Console.WriteLine("[97m-[0m");
                Console.SetWindowSize(150, 40);
                Console.ReadLine();
                return; 
            }
        }

        private static void GetArgs(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
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

        private static void ParallelFor(int from, int to, int threads, CancellationToken cancel, Action<int> func)
        {
            unsafe
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
}
