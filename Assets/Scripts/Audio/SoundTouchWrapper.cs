using System;
using System.Runtime.InteropServices;

public class SoundTouchWrapper : IDisposable
{
    private const string DLL_NAME = "SoundTouch";
    private IntPtr handle;

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr soundtouch_createInstance();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_destroyInstance(IntPtr handle);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setSampleRate(IntPtr handle, int srate);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setChannels(IntPtr handle, int numChannels);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setTempo(IntPtr handle, float tempo);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setPitchSemiTones(IntPtr handle, float pitch);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_putSamples(IntPtr handle, float[] samples, int numSamples);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int soundtouch_receiveSamples(IntPtr handle, float[] outBuffer, int maxSamples);

    public SoundTouchWrapper()
    {
        handle = soundtouch_createInstance();
    }

    public void SetSampleRate(int srate) => soundtouch_setSampleRate(handle, srate);
    public void SetChannels(int channels) => soundtouch_setChannels(handle, channels);
    public void SetTempo(float tempo) => soundtouch_setTempo(handle, tempo);
    public void SetPitch(float pitch) => soundtouch_setPitchSemiTones(handle, pitch);
    public void PutSamples(float[] samples, int count) => soundtouch_putSamples(handle, samples, count);
    public int ReceiveSamples(float[] buffer, int maxCount) => soundtouch_receiveSamples(handle, buffer, maxCount);

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            soundtouch_destroyInstance(handle);
            handle = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~SoundTouchWrapper()
    {
        Dispose();
    }
}
