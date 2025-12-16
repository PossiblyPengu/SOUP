using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SOUP.Helpers;

/// <summary>
/// Helper class for sending raw data directly to a printer (e.g., ZPL to Zebra printers)
/// </summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Send raw bytes directly to a printer
    /// </summary>
    /// <param name="printerName">The name of the printer</param>
    /// <param name="bytes">The raw bytes to send</param>
    /// <param name="documentName">Optional document name for the print job</param>
    /// <returns>True if successful</returns>
    public static bool SendBytesToPrinter(string printerName, byte[] bytes, string documentName = "Raw Document")
    {
        IntPtr hPrinter = IntPtr.Zero;
        var di = new DOCINFOA
        {
            pDocName = documentName,
            pDataType = "RAW"
        };

        bool success = false;

        if (OpenPrinter(printerName.Normalize(), out hPrinter, IntPtr.Zero))
        {
            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
                        success = WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out _);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }

        return success;
    }

    /// <summary>
    /// Send a string directly to a printer (for ZPL commands)
    /// </summary>
    /// <param name="printerName">The name of the printer</param>
    /// <param name="data">The string data to send (e.g., ZPL commands)</param>
    /// <param name="documentName">Optional document name for the print job</param>
    /// <returns>True if successful</returns>
    public static bool SendStringToPrinter(string printerName, string data, string documentName = "ZPL Label")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        return SendBytesToPrinter(printerName, bytes, documentName);
    }
}
