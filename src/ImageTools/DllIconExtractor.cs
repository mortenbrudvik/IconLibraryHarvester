using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageTools;

public class DllIconExtractor
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

    private static readonly IntPtr RT_ICON = (IntPtr) 3;
    private static readonly IntPtr RT_GROUP_ICON = (IntPtr) 14;

    private const int MAX_PATH = 260;

    private byte[][]? iconData;

    public string FileName { get; private set; }

    public int Count => iconData.Length;

    public DllIconExtractor(string fileName)
    {
        Initialize(fileName);
    }

    public Icon GetIcon(int index)
    {
        if (index < 0 || Count <= index)
            throw new ArgumentOutOfRangeException("index");

        // Create an Icon from the .ico file in memory.

        using (var ms = new MemoryStream(iconData[index]))
        {
            return new Icon(ms);
        }
    }

    public Icon[] GetAllIcons()
    {
        var icons = new List<Icon>();
        for (int i = 0; i < Count; ++i)
            icons.Add(GetIcon(i));

        return icons.ToArray();
    }

    public void Save(int index, Stream outputStream)
    {
        if (index < 0 || Count <= index)
            throw new ArgumentOutOfRangeException("index");

        if (outputStream == null)
            throw new ArgumentNullException("outputStream");

        var data = iconData[index];
        outputStream.Write(data, 0, data.Length);
    }

    private void Initialize(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var hModule = IntPtr.Zero;
        try
        {
            hModule = NativeMethods.LoadLibraryEx(fileName, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hModule == IntPtr.Zero)
                throw new Win32Exception();

            FileName = GetFileName(hModule);

            var tmpData = new List<byte[]>();

            ENUMRESNAMEPROC callback = (h, t, name, l) =>
            {
                // Refer to the following URL for the data structures used here:
                // http://msdn.microsoft.com/en-us/library/ms997538.aspx

                // RT_GROUP_ICON resource consists of a GRPICONDIR and GRPICONDIRENTRY's.

                var dir = GetDataFromResource(hModule, RT_GROUP_ICON, name);

                // Calculate the size of an entire .icon file.

                int count = BitConverter.ToUInt16(dir, 4); // GRPICONDIR.idCount
                var len = 6 + 16 * count; // sizeof(ICONDIR) + sizeof(ICONDIRENTRY) * count
                for (var i = 0; i < count; ++i)
                    len += BitConverter.ToInt32(dir, 6 + 14 * i + 8); // GRPICONDIRENTRY.dwBytesInRes

                using var dst = new BinaryWriter(new MemoryStream(len));
                // Copy GRPICONDIR to ICONDIR.

                dst.Write(dir, 0, 6);

                var picOffset = 6 + 16 * count; // sizeof(ICONDIR) + sizeof(ICONDIRENTRY) * count

                for (var i = 0; i < count; ++i)
                {
                    // Load the picture.

                    var id = BitConverter.ToUInt16(dir, 6 + 14 * i + 12); // GRPICONDIRENTRY.nID
                    var pic = GetDataFromResource(hModule, RT_ICON, (IntPtr) id);

                    // Copy GRPICONDIRENTRY to ICONDIRENTRY.

                    dst.Seek(6 + 16 * i, SeekOrigin.Begin);

                    dst.Write(dir, 6 + 14 * i, 8); // First 8bytes are identical.
                    dst.Write(pic.Length); // ICONDIRENTRY.dwBytesInRes
                    dst.Write(picOffset); // ICONDIRENTRY.dwImageOffset

                    // Copy a picture.

                    dst.Seek(picOffset, SeekOrigin.Begin);
                    dst.Write(pic, 0, pic.Length);

                    picOffset += pic.Length;
                }

                tmpData.Add(((MemoryStream) dst.BaseStream).ToArray());

                return true;
            };
            NativeMethods.EnumResourceNames(hModule, RT_GROUP_ICON, callback, IntPtr.Zero);

            iconData = tmpData.ToArray();
        }
        finally
        {
            if (hModule != IntPtr.Zero)
                NativeMethods.FreeLibrary(hModule);
        }
    }

    private byte[] GetDataFromResource(IntPtr hModule, IntPtr type, IntPtr name)
    {
        // Load the binary data from the specified resource.

        IntPtr hResInfo = NativeMethods.FindResource(hModule, name, type);
        if (hResInfo == IntPtr.Zero)
            throw new Win32Exception();

        IntPtr hResData = NativeMethods.LoadResource(hModule, hResInfo);
        if (hResData == IntPtr.Zero)
            throw new Win32Exception();

        IntPtr pResData = NativeMethods.LockResource(hResData);
        if (pResData == IntPtr.Zero)
            throw new Win32Exception();

        uint size = NativeMethods.SizeofResource(hModule, hResInfo);
        if (size == 0)
            throw new Win32Exception();

        byte[] buf = new byte[size];
        Marshal.Copy(pResData, buf, 0, buf.Length);

        return buf;
    }

    private string GetFileName(IntPtr hModule)
    {
        // Alternative to GetModuleFileName() for the module loaded with
        // LOAD_LIBRARY_AS_DATAFILE option.

        // Get the file name in the format like:
        // "\\Device\\HarddiskVolume2\\Windows\\System32\\shell32.dll"

        string fileName;
        {
            var buf = new StringBuilder(MAX_PATH);
            int len = NativeMethods.GetMappedFileName(
                NativeMethods.GetCurrentProcess(), hModule, buf, buf.Capacity);
            if (len == 0)
                throw new Win32Exception();

            fileName = buf.ToString();
        }

        // Convert the device name to drive name like:
        // "C:\\Windows\\System32\\shell32.dll"

        for (char c = 'A'; c <= 'Z'; ++c)
        {
            var drive = c + ":";
            var buf = new StringBuilder(MAX_PATH);
            int len = NativeMethods.QueryDosDevice(drive, buf, buf.Capacity);
            if (len == 0)
                continue;

            var devPath = buf.ToString();
            if (fileName.StartsWith(devPath))
                return (drive + fileName.Substring(devPath.Length));
        }

        return fileName;
    }
}

