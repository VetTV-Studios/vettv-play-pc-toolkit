using System.Text;

namespace VetTV.PlayPC.Core.Launcher;

/// <summary>Minimal reader/writer for Steam's binary KeyValues (used by shortcuts.vdf). Types: 0 map, 1 string, 2 int32, 8 end.</summary>
public sealed class VdfNode
{
    public List<KeyValuePair<string, object>> Items { get; } = new();

    public object? this[string key]
    {
        get => Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        set
        {
            var idx = Items.FindIndex(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) Items[idx] = new(Items[idx].Key, value!); else Items.Add(new(key, value!));
        }
    }

    public static VdfNode Read(Stream s)
    {
        using var r = new BinaryReader(s, Encoding.UTF8, leaveOpen: true);
        var root = new VdfNode();
        ReadInto(r, root);
        return root;
    }

    private static void ReadInto(BinaryReader r, VdfNode node)
    {
        while (r.BaseStream.Position < r.BaseStream.Length)
        {
            var type = r.ReadByte();
            if (type == 8) return;
            var key = ReadString(r);
            switch (type)
            {
                case 0: var child = new VdfNode(); ReadInto(r, child); node.Items.Add(new(key, child)); break;
                case 1: node.Items.Add(new(key, ReadString(r))); break;
                case 2: node.Items.Add(new(key, r.ReadInt32())); break;
                default: throw new InvalidDataException($"Unsupported VDF type {type}.");
            }
        }
    }

    private static string ReadString(BinaryReader r)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = r.ReadByte()) != 0) bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public void Write(Stream s)
    {
        using var w = new BinaryWriter(s, Encoding.UTF8, leaveOpen: true);
        WriteItems(w, this);
        w.Write((byte)8);
    }

    private static void WriteItems(BinaryWriter w, VdfNode node)
    {
        foreach (var (k, v) in node.Items)
        {
            switch (v)
            {
                case VdfNode n: w.Write((byte)0); WriteStr(w, k); WriteItems(w, n); w.Write((byte)8); break;
                case string str: w.Write((byte)1); WriteStr(w, k); WriteStr(w, str); break;
                case int i: w.Write((byte)2); WriteStr(w, k); w.Write(i); break;
                default: throw new InvalidDataException($"Unsupported value for '{k}'.");
            }
        }
    }

    private static void WriteStr(BinaryWriter w, string s) { w.Write(Encoding.UTF8.GetBytes(s)); w.Write((byte)0); }
}
