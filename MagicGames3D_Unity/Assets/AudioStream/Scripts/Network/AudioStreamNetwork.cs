// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// 
    /// </summary>
    public abstract partial class AudioStreamNetwork : MonoBehaviour
    {
        public const string localhost = "127.0.0.1";
        /// <summary>
        /// 
        /// </summary>
        public enum CODEC : byte
        {
            OPUS
                , PCM
        }
        /// <summary>
        /// Encoder transport samplerate (used for transfer, regardless of actual sample rate)
        /// </summary>
        public const int opusSampleRate = 48000;
        public const int opusChannels = 2;
        /// <summary>
        /// 
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PAYLOAD
        {
            public CODEC codec;
            public UInt16 samplerate;
            public UInt16 channels;
            // Marshal. should assume endianness.>.
            // public bool isLittleEndian;
        }

        protected byte[] PayloadToBytes(PAYLOAD payload)
        {
            var size = Marshal.SizeOf(payload);
            var result = new byte[size];

            GCHandle h = default(GCHandle);
            try
            {
                h = GCHandle.Alloc(result, GCHandleType.Pinned);
                Marshal.StructureToPtr<PAYLOAD>(payload, h.AddrOfPinnedObject(), false);
            }
            catch
            {
                result = null;
            }
            finally
            {
                if (h.IsAllocated)
                    h.Free();
            }

            return result;
        }

        protected PAYLOAD BytesToPayload(byte[] bytes)
        {
            var result = default(PAYLOAD);
            var h = default(GCHandle);

            try
            {
                h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                result = Marshal.PtrToStructure<PAYLOAD>(h.AddrOfPinnedObject());
            }
            catch
            {
                result = default(PAYLOAD);
            }
            finally
            {
                if (h.IsAllocated)
                    h.Free();
            }

            return result;
        }
    }
}