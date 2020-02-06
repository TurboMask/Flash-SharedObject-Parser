/* SharedObject parser. Only primitive types are implemented (bool, int, double, string).
 * Code by Arvydas B.
 * 
 * SharedObject specification:
 * https://en.wikipedia.org/wiki/Action_Message_Format
 * https://github.com/jamesward/JSAMF/blob/master/web/web/amf.js
 */

using System;
using System.IO;
using System.Collections.Generic;

public class SharedObject
{
    public List<SOValue> values;

    public SharedObject()
    {
        values = new List<SOValue>();
    }

    public SOValue Get(string keyword)
    {
        for(int i = 0; i < values.Count; i++)
        {
            if(values[i].key == keyword)
            {
                return values[i];
            }
        }
        return new SOValue();   //Return UNDEFINED
    }
}

public struct SOValue
{
    public string key;
    public byte type;
    public string string_val;
    public bool bool_val;
    public int int_val;
    public double double_val;
}

class SOReader
{
    public byte[] file_data;
    public int file_size;
    public int pos;

    public SOReader(string filename)
    {
        file_data = File.ReadAllBytes(filename);
        file_size = 0;
        pos = 0;
    }

    public byte Read8()
    {
        return file_data[pos++];
    }

    public UInt16 Read16()
    {
        UInt16 val = file_data[pos++];
        val = (UInt16)((val << 8) | file_data[pos++]);
        return val;
    }

    public UInt32 Read32()
    {
        UInt32 val = 0;
        for (int i = 0; i < 4; i++)
        {
            val = (UInt32)((val << 8) | file_data[pos++]);
        }
        return val;
    }

    public Int32 ReadCompressedInt()
    {
        Int32 val = 0;
        byte part = 0;
        bool finished = true;
        Int32 data_bytes = 0;
        for (int i = 0; i < 3; i++)
        {
            part = Read8();
            finished = (part & 0x80) == 0;
            val = (val << 7);
            val |= (Int32)(part & 0b01111111);
            data_bytes += 7;
            if (finished)
            {
                break;
            }
        }
        if (!finished)
        {
            part = Read8();
            val = (val << 8);
            val |= (Int32)part;
            data_bytes += 8;
        }
        //Check if number is negative. Only numbers with 29 data bytes can be negative.
        if (val >> (data_bytes - 1) == 1 && data_bytes == 29)
        {
            val = (Int32)(-(~(val | (0xFFFFFFFF << data_bytes)) + 1));
        }
        return val;
    }

    public double ReadDouble()
    {
        byte[] double_raw = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            double_raw[i] = file_data[pos + 7 - i];
        }
        pos += 8;
        double val = BitConverter.ToDouble(double_raw, 0);
        return val;
    }

    public string ReadString(int length)
    {
        string val = System.Text.Encoding.UTF8.GetString(file_data, pos, length);
        pos += length;
        return val;
    }
}

struct SOHeader
{
    public UInt16 padding1;
    public UInt32 file_size;
    public UInt32 padding2;
    public UInt16 padding3;
    public UInt32 padding4;
}

struct SOTypes
{
    public const byte TYPE_UNDEFINED = 0x00;
    public const byte TYPE_NULL = 0x01;
    public const byte TYPE_BOOL_FALSE = 0x02;
    public const byte TYPE_BOOL_TRUE = 0x03;
    public const byte TYPE_INT = 0x04;
    public const byte TYPE_DOUBLE = 0x05;
    public const byte TYPE_STRING = 0x06;

    /*
    Undefined - 0x00
    Null - 0x01
    Boolean False - 0x02
    Boolean True - 0x03
    Integer - 0x04 (expandable 8+ bit integer)
    Double - 0x05 (Encoded as IEEE 64-bit double-precision floating point number)
    String - 0x06 (expandable 8+ bit integer string length with a UTF-8 string)
    XML - 0x07 (expandable 8+ bit integer string length and/or flags with a UTF-8 string)
    Date - 0x08 (expandable 8+ bit integer flags with an IEEE 64-bit double-precision floating point UTC offset time)
    Array - 0x09 (expandable 8+ bit integer entry count and/or flags with optional expandable 8+ bit integer name lengths with a UTF-8 names)
    Object - 0x0A (expandable 8+ bit integer entry count and/or flags with optional expandable 8+ bit integer name lengths with a UTF-8 names)
    XML End - 0x0B (expandable 8+ bit integer flags)
    ByteArray - 0x0C (expandable 8+ bit integer flags with optional 8 bit byte length)
    VectorInt - 0x0D
    VectorUInt - 0x0E
    VectorDouble - 0x0F
    VectorObject - 0x10
    Dictionary - 0x11
    The first 4 types are not followed by any data(Booleans have two types in AMF3).
    */
}

public class SharedObjectParser
{
    public static SharedObject Parse(string filename, SharedObject so = null)
    {
        if (so == null)
        {
            so = new SharedObject();
        }
        if (!File.Exists(filename))
        {
            Console.WriteLine("SharedObject " + filename + " doesn't exist.");
            return so;
        }
        SOReader file = new SOReader(filename);
        List<string> string_table = new List<string>();

        //Read header
        SOHeader header = new SOHeader();
        header.padding1 = file.Read16();
        header.file_size = file.Read32();
        file.file_size = (int)header.file_size + 6;
        header.padding2 = file.Read32();
        header.padding3 = file.Read16();
        header.padding4 = file.Read32();
        Console.WriteLine("Data size: " + header.file_size);

        //Read SO name and othe rparameters
        UInt16 so_name_length = file.Read16();
        string so_name = file.ReadString(so_name_length);
        //string_table.Add(so_name);
        Console.WriteLine("SO name: " + so_name);
        UInt32 so_type = file.Read32();
        Console.WriteLine("SO type: " + so_type);

        while (file.pos < file.file_size)
        {
            SOValue so_value = new SOValue();

            // Read parameter name. Name length is encoded into 7 bits, 8th bit is flag if name is inline or indexed.
            UInt32 length_int = (UInt32)file.ReadCompressedInt();
            bool name_inline = (length_int & 0x01) > 0;
            length_int >>= 1;
            if (name_inline)
            {
                so_value.key = file.ReadString((int)length_int);
                string_table.Add(so_value.key);
            }
            else
            {
                so_value.key = string_table[(int)length_int];
            }
            Console.WriteLine(so_value.key + " (inline: " + name_inline + ")");

            // Read parameter value. First byte is value type.
            so_value.type = file.Read8();
            if(so_value.type == SOTypes.TYPE_NULL)
            {
                Console.WriteLine("\tNULL");
            }
            else if (so_value.type == SOTypes.TYPE_BOOL_FALSE)
            {
                so_value.bool_val = false;
                Console.WriteLine("\tFalse");
            }
            else if (so_value.type == SOTypes.TYPE_BOOL_TRUE)
            {
                so_value.bool_val = true;
                Console.WriteLine("\tTrue");
            }
            else if (so_value.type == SOTypes.TYPE_INT)
            {
                so_value.int_val = (int)file.ReadCompressedInt();
                Console.WriteLine("\t" + so_value.int_val);
            }
            else if (so_value.type == SOTypes.TYPE_DOUBLE)
            {
                so_value.double_val = file.ReadDouble();
                Console.WriteLine("\t" + so_value.double_val);
            }
            else if (so_value.type == SOTypes.TYPE_STRING)
            {
                Int32 val_length = file.ReadCompressedInt();
                bool val_inline = (val_length & 0x00000001) > 0;
                val_length >>= 1;
                if (!val_inline)
                {
                    Console.WriteLine("\tReference to string: " + val_length);
                    if (val_length < string_table.Count)
                    {
                        so_value.string_val = string_table[(int)val_length];
                        Console.WriteLine("\t" + so_value.string_val);
                    }
                }
                else
                {
                    so_value.string_val = file.ReadString((int)val_length);
                    string_table.Add(so_value.string_val);
                    Console.WriteLine("\t" + so_value.string_val + " (" + val_length + ")");
                }
            }
            else
            {
                Console.WriteLine("Type not implemented yet: " + so_value.type);
                //Move read position to next item
                while (file.pos < file.file_size)
                {
                    byte next_byte = file.Read8();
                    if(next_byte == 0)
                    {
                        --file.pos;
                        break;
                    }
                }
            }
            so.values.Add(so_value);
            if(file.pos < file.file_size)
            {
                file.Read8();   //Padding
            }
        }
        return so;
    }
}