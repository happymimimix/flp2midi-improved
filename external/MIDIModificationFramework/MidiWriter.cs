using MIDIModificationFramework.MIDIEvents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MIDIModificationFramework
{
    public class MidiWriter
    {
        Stream writer;

        long chunkStart = 0;

        int tracks = 0;

        public MidiWriter(Stream writer)
        {
            this.writer = writer;
        }

        public MidiWriter(string output) : this(new BufferedStream(File.Open(output, FileMode.Create)))
        { }

        public void Write(string text)
        {
            unsafe
            {
                unchecked
                {
                    for (int i = 0; i < text.Length; i++) writer.WriteByte((byte)text[i]);
                }
            }
        }

        public void Write(MIDIEvent e)
        {
            unsafe
            {
                unchecked
                {
                    var data = e.GetDataWithDelta();
                    writer.Write(data, 0, data.Length);
                }
            }
        }

        public void Write(IEnumerable<MIDIEvent> seq)
        {
            unsafe
            {
                unchecked
                {
                    foreach (var e in seq)
                    {
                        var data = e.GetDataWithDelta();
                        writer.Write(data, 0, data.Length);
                    }
                }
            }
        }

        public void Write(byte[] data)
        {
            unsafe
            {
                unchecked
                {
                    writer.Write(data, 0, data.Length);
                }
            }
        }

        public void Write(Stream data)
        {
            unsafe
            {
                unchecked
                {
                    var bytes = new byte[4096];
                    int read;

                    while ((read = data.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        writer.Write(bytes, 0, read);
                    }
                    //data.CopyTo(writer);
                }
            }
        }

        public void Write(ushort v)
        {
            unsafe
            {
                unchecked
                {
                    for (int i = 1; i >= 0; i--) writer.WriteByte((byte)((v >> (i * 8)) & 0xFF));
                }
            }
        }

        public void Write(uint v)
        {
            unsafe
            {
                unchecked
                {
                    for (int i = 3; i >= 0; i--) writer.WriteByte((byte)((v >> (i * 8)) & 0xFF));
                }
            }
        }

        public void Write(byte v)
        {
            unsafe
            {
                unchecked
                {
                    writer.WriteByte(v);
                }
            }
        }

        public void WriteTrack(IEnumerable<MIDIEvent> seq)
        {
            InitTrack();
            Write(seq);
            EndTrack();
        }

        public void WriteTrack(byte[] data)
        {
            InitTrack();
            Write(data);
            EndTrack();
        }

        public void WriteTrack(Stream data)
        {
            InitTrack();
            Write(data);
            EndTrack();
        }

        public void WriteVariableLen(int i)
        {
            unsafe
            {
                unchecked
                {
                    var b = new byte[5];
                    int len = 0;
                    while (true)
                    {
                        byte v = (byte)(i & 0x7F);
                        i = i >> 7;
                        if (i != 0)
                        {
                            v = (byte)(v | 0x80);
                            b[len++] = v;
                        }
                        else
                        {
                            b[len++] = v;
                            break;
                        }
                    }
                    Write(b.Take(len).ToArray());
                }
            }
        }

        public void WriteFormat(ushort s)
        {
            unsafe
            {
                unchecked
                {
                    long pos = writer.Position;
                    writer.Position = 8;
                    Write((ushort)s);
                    writer.Position = pos;
                }
            }
        }

        void WriteNtrks(ushort s)
        {
            unsafe
            {
                unchecked
                {
                    long pos = writer.Position;
                    writer.Position = 10;
                    Write((ushort)s);
                    writer.Position = pos;
                }
            }
        }

        void WritePPQ(ushort s)
        {
            unsafe
            {
                unchecked
                {
                    long pos = writer.Position;
                    writer.Position = 12;
                    Write((ushort)s);
                    writer.Position = pos;
                }
            }
        }

        public void Init(ushort ppq)
        {
            unsafe
            {
                unchecked
                {
                    writer.Position = 0;
                    Write("MThd");
                    Write((uint)6);
                    WriteFormat(1);
                    WriteNtrks(0);
                    WritePPQ(ppq);
                }
            }
        }

        public void InitTrack()
        {
            unsafe
            {
                unchecked
                {
                    chunkStart = writer.Length;
                    writer.Position = chunkStart;
                    Write("MTrk");
                    Write((uint)0);
                }
            }
        }

        public void EndTrack()
        {
            unsafe
            {
                unchecked
                {
                    Write(new byte[] { 0, 0xFF, 0x2F, 0x00 });
                    uint len = (uint)(writer.Position - chunkStart) - 8;
                    writer.Position = chunkStart + 4;
                    Write(len);
                    writer.Position = writer.Length;
                    tracks++;
                }
            }
        }

        public void Close()
        {
            unsafe
            {
                unchecked
                {
                    if (tracks > 65535) tracks = 65535;
                    WriteNtrks((ushort)tracks);
                    writer.Flush();
                    writer.Close();
                }
            }
        }
    }
}
