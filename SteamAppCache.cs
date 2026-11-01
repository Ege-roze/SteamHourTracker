namespace SteamHourTracker;

// Read only entry IDs; names/ownership are obtained from the live client.
// Length-prefixed entries let us skip the binary KeyValues payload and token fields.
internal static class SteamAppCache
{
    public static List<uint> ReadAppIds(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 12) throw new InvalidDataException(UiText.Get("Steam oyun önbelleği eksik."));
        uint version = reader.ReadUInt32();
        if (version is not (0x07564427 or 0x07564428 or 0x07564429))
            throw new InvalidDataException(UiText.Get("Steam oyun önbelleğinin bu sürümü henüz desteklenmiyor."));
        reader.ReadUInt32(); // universe
        long entriesEnd = stream.Length;
        if (version == 0x07564429)
        {
            if (stream.Length < 20) throw new InvalidDataException(UiText.Get("Steam oyun önbelleği eksik."));
            entriesEnd = reader.ReadInt64();
            if (entriesEnd < 20 || entriesEnd > stream.Length)
                throw new InvalidDataException(UiText.Get("Steam oyun önbelleği güncelleniyor. Biraz sonra yeniden dene."));
        }
        var ids = new HashSet<uint>();
        while (stream.Position + 4 <= entriesEnd)
        {
            uint id = reader.ReadUInt32();
            if (id == 0) return ids.Order().ToList();
            if (stream.Position + 4 > entriesEnd) break;
            uint size = reader.ReadUInt32();
            uint minimumSize = version == 0x07564427 ? 40u : 60u;
            if (size < minimumSize || size > entriesEnd - stream.Position)
                throw new InvalidDataException(UiText.Get("Steam oyun önbelleği eksik veya güncelleniyor. Yenile'ye tekrar bas."));
            ids.Add(id);
            if (ids.Count > 2_000_000) throw new InvalidDataException(UiText.Get("Steam önbelleği beklenen boyutu aşıyor."));
            stream.Seek(size, SeekOrigin.Current);
        }
        throw new InvalidDataException(UiText.Get("Steam oyun önbelleği tamamlanmamış. Yenile'ye tekrar bas."));
    }
}
