using System.Runtime.InteropServices;

namespace OrientExpress.Spike007;

/// <summary>
/// Authoritative installed-codec inventory via WIC's component enumerator.
/// The registry Instance categories list only add-on codecs; inbox codecs
/// (JPEG/PNG/BMP/GIF/TIFF/HEIF/WebP) surface only through this COM path —
/// itself a spike finding worth recording.
/// </summary>
public static class WicComInventory
{
    private const int WicDecoder = 0x1;
    private const int WicEncoder = 0x2;

    public static IReadOnlyList<string> DecoderFriendlyNames() => FriendlyNames(WicDecoder);

    public static IReadOnlyList<string> EncoderFriendlyNames() => FriendlyNames(WicEncoder);

    private static List<string> FriendlyNames(int componentType)
    {
        var names = new List<string>();
        var factory = (IWICImagingFactory)new WicImagingFactoryComObject();
        try
        {
            factory.CreateComponentEnumerator(componentType, 0, out var enumerator);
            try
            {
                var buffer = new object?[1];
                while (enumerator.Next(1, buffer, out var fetched) == 0 && fetched == 1)
                {
                    if (buffer[0] is IWICComponentInfo info)
                    {
                        names.Add(ReadFriendlyName(info));
                        _ = Marshal.ReleaseComObject(info);
                    }

                    buffer[0] = null;
                }
            }
            finally
            {
                _ = Marshal.ReleaseComObject(enumerator);
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(factory);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string ReadFriendlyName(IWICComponentInfo info)
    {
        info.GetFriendlyName(0, null, out var length);
        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        info.GetFriendlyName(length, buffer, out var actual);
        return new string(buffer, 0, (int)Math.Max(actual - 1, 0));
    }

    [ComImport]
    [Guid("cacaf262-9370-4615-a13b-9f5539da4c0a")]
    private class WicImagingFactoryComObject
    {
    }

    [ComImport]
    [Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICImagingFactory
    {
        // Vtable slots 4..23 unused by this harness; order must be preserved.
        public void Slot04();
        public void Slot05();
        public void Slot06();
        public void Slot07();
        public void Slot08();
        public void Slot09();
        public void Slot10();
        public void Slot11();
        public void Slot12();
        public void Slot13();
        public void Slot14();
        public void Slot15();
        public void Slot16();
        public void Slot17();
        public void Slot18();
        public void Slot19();
        public void Slot20();
        public void Slot21();
        public void Slot22();
        public void Slot23();

        public void CreateComponentEnumerator(int componentTypes, int options, out IEnumUnknown enumerator);
    }

    [ComImport]
    [Guid("00000100-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumUnknown
    {
        [PreserveSig]
        public int Next(
            int count,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown, SizeParamIndex = 0)] object?[] items,
            out int fetched);
    }

    [ComImport]
    [Guid("23BC3F0A-698B-4357-886B-F24D50671334")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICComponentInfo
    {
        public void GetComponentType(out int type);

        public void GetCLSID(out Guid clsid);

        public void GetSigningStatus(out int status);

        public void GetAuthor(uint cchAuthor, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[]? author, out uint actual);

        public void GetVendorGUID(out Guid vendor);

        public void GetVersion(uint cchVersion, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[]? version, out uint actual);

        public void GetSpecVersion(uint cchSpec, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[]? spec, out uint actual);

        public void GetFriendlyName(uint cchName, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[]? name, out uint actual);
    }
}
