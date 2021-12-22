using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.Models
{
    public class FileIO
    {
        //write all details of an app to a file
        public static void WriteFile(MyAppInfo app, FileStream stream)
        {
            int size = 8 + 1 + app.Name.Length;

            byte[] Bytes = new byte[size];

            int byteIndex = 7;

            for (int i = byteIndex; i >= 0; i--)
                Bytes[i] = (byte)(app.DataRecv >> 8 * i);

            Bytes[byteIndex + 1] = (byte)(app.Name.Length);

            for (int i = 0; i < app.Name.Length; i++)
                Bytes[byteIndex + 2 + i] = (byte)app.Name[i];

            stream.Write(Bytes, 0, Bytes.Length);
        }

        //read the file data into a collection
        public static void ReadFile(FileStream stream)
        {
            int breakLength = 0;
            while (true)
            {
                int arraySize = 8;
                byte[] Bytes = new byte[arraySize];
                breakLength += arraySize;
                for (int i = 0; i < arraySize; i++)
                {
                    Bytes[i] = (byte)stream.ReadByte();
                    Console.WriteLine(Bytes[i]);
                }
                Console.WriteLine("converted bytes to int: " + BitConverter.ToUInt64(Bytes, 0));
                breakLength += 1;
                int nameLength = stream.ReadByte();
                byte[] Bytes1 = new byte[nameLength];
                breakLength += nameLength;
                for (int i = 0; i < nameLength; i++)
                {
                    Bytes1[i] = (byte)stream.ReadByte();
                    Console.WriteLine(Bytes1[i]);
                }
                for (int i = 0; i < Bytes1.Length; i++)
                    Console.WriteLine("converted bytes to char: " + (char)Bytes1[i]);

                Console.WriteLine("\n");
                if (breakLength >= stream.Length)
                {
                    break;
                }

            }
        }

    }
}
