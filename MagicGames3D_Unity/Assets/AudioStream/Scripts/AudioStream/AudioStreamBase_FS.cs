// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using FMOD;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// FMOD custom filesystem (setFileSystem callbacks) and download buffer
    /// </summary>
    public abstract partial class AudioStreamBase : MonoBehaviour
    {
        /*
         * FMOD file system
         */
        public DownloadFileSystemBase mediaBuffer { get; protected set; }
        // ========================================================================================================================================
        #region FMOD filesystem callbacks
        readonly static object fmod_callback_lock = new object();
        /*
            File callbacks
            due to IL2CPP not supporting marshaling delegates that point to instance methods to native code we need to circumvent this via static dispatch
            (similarly as for exinfo for output device )
        */
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_OPEN_CALLBACK))]
        static RESULT Media_Open(IntPtr name, ref uint filesize, ref IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarning("Media_Open userdata == 0");
                    return RESULT.ERR_INVALID_HANDLE;
                }

                var objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                // for size of the media the assumption is that in case of local file the returned size is actual file size/length on complete read
                // and network will deliver everything later
                switch (audioStream.mediaType)
                {
                    case MEDIATYPE.NETWORK:
                        // will 4GB stream be always enough, question mark
                        filesize = audioStream.downloadHandler.contentLength;
                        audioStream.mediaLength = filesize;
                        audioStream.mediaAvailable = 0;
                        break;

                    case MEDIATYPE.FILESYSTEM:
                        filesize = audioStream.downloadHandler.contentLength;
                        audioStream.mediaLength = audioStream.mediaDownloaded = audioStream.mediaAvailable = filesize;
                        break;

                    case MEDIATYPE.MEMORY:
                        filesize = (audioStream as AudioStreamMemory).memoryLength;
                        audioStream.mediaLength = audioStream.mediaDownloaded = audioStream.mediaAvailable = filesize;

                        break;
                }

                // handle not used, can be something like local file handle, but we're using UWR anyway
                handle = IntPtr.Zero;

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_open ---------------");

                return FMOD.RESULT.OK;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_CLOSE_CALLBACK))]
        static FMOD.RESULT Media_Close(IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_Close userdata == 0");
                    return RESULT.OK;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_close ---------------");

                return FMOD.RESULT.OK;
            }
        }
        /// <summary>
        /// See below
        /// </summary>
        FMOD.RESULT media_read_lastResult = FMOD.RESULT.OK;
        /*
         * async version of the API
        */
        /// <summary>
        /// flag for Marshal.StructureToPtr
        /// https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.structuretoptr?view=netframework-4.8
        /// If you use the StructureToPtr<T>(T, IntPtr, Boolean) method to copy a different instance to the memory block at a later time, specify true for fDeleteOld to remove reference counts for reference types in the previous instance. Otherwise, the managed reference types and unmanaged copies are effectively leaked.
        /// </summary>
        // bool fDeleteOld = false;
        /// <summary>
        /// Retrieves any encoded data based on info and immediately satisfies read request
        /// Sets 'media_read_lastResult' for main thread to detect media data shortage
        /// <param name="infoptr"></param>
        /// <param name="userdata"></param>
        /// <returns>FMOD.RESULT.OK if all requested bytes were read</returns>
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCREAD_CALLBACK))]
        static FMOD.RESULT Media_AsyncRead(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncRead userdata == 0");
                    info.done(infoptr, RESULT.ERR_INVALID_HANDLE);
                    return RESULT.ERR_INVALID_HANDLE;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);
                if (!audioStream)
                {
                    info.done(infoptr, FMOD.RESULT.ERR_FILE_EOF);
                    return FMOD.RESULT.ERR_FILE_EOF;
                }

                // shutdown
                if (!audioStream
                    || audioStream.mediaBuffer == null
                    )
                {
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                    info.done(infoptr, audioStream.media_read_lastResult);
                    return audioStream.media_read_lastResult;
                }

                var downloadBytes = audioStream.mediaBuffer.Read(info.offset, info.sizebytes, audioStream.mediaLength);
                Marshal.Copy(downloadBytes, 0, info.buffer, downloadBytes.Length);

                info.bytesread = (uint)downloadBytes.Length;

                if (info.bytesread < info.sizebytes)
                {
                    audioStream.LOG(LogLevel.INFO, "FED     {0}/{1} bytes, offset {2} (* EOF)", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                }
                else
                {
                    audioStream.LOG(LogLevel.DEBUG, "FED     {0}/{1} bytes, offset {2}", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.OK;
                }

                // update the unmanaged data (here esp. bytesread)
                Marshal.StructureToPtr(info, infoptr, false);

                info.done(infoptr, audioStream.media_read_lastResult);

                return audioStream.media_read_lastResult;
            }
        }
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCCANCEL_CALLBACK))]
        static FMOD.RESULT Media_AsyncCancel(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncCancel userdata == 0");
                    info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                    return RESULT.ERR_FILE_DISKEJECTED;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_asynccancel --------------- {0}", infoptr);

                if (infoptr == IntPtr.Zero)
                {
                    info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                    return FMOD.RESULT.ERR_FILE_DISKEJECTED;
                }

                // Signal FMOD to wake up, this operation has been cancelled

                // Debug.LogFormat("CANCEL {0} bytes, offset {1} PRIORITY = {2}.", info_deptr.sizebytes, info_deptr.offset, info_deptr.priority);

                info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                return FMOD.RESULT.ERR_FILE_DISKEJECTED;
            }
        }
        FMOD.FILE_OPEN_CALLBACK fileOpenCallback;
        FMOD.FILE_CLOSE_CALLBACK fileCloseCallback;
        // async version of the API
        FMOD.FILE_ASYNCREAD_CALLBACK fileAsyncReadCallback;
        FMOD.FILE_ASYNCCANCEL_CALLBACK fileAsyncCancelCallback;
        #endregion
    }
}