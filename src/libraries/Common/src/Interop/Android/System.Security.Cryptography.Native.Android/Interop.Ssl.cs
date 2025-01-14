// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Win32.SafeHandles;

using SafeSslHandle = System.Net.SafeSslHandle;

internal static partial class Interop
{
    internal static partial class AndroidCrypto
    {
        internal unsafe delegate PAL_SSLStreamStatus SSLReadCallback(byte* data, int* length);
        internal unsafe delegate void SSLWriteCallback(byte* data, int length);

        internal enum PAL_SSLStreamStatus
        {
            OK = 0,
            NeedData = 1,
            Error = 2,
            Renegotiate = 3,
            Closed = 4,
        };

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamCreate")]
        internal static extern SafeSslHandle SSLStreamCreate();

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamCreateWithCertificates")]
        private static extern SafeSslHandle SSLStreamCreateWithCertificates(
            ref byte pkcs8PrivateKey,
            int pkcs8PrivateKeyLen,
            PAL_KeyAlgorithm algorithm,
            IntPtr[] certs,
            int certsLen);
        internal static SafeSslHandle SSLStreamCreateWithCertificates(ReadOnlySpan<byte> pkcs8PrivateKey, PAL_KeyAlgorithm algorithm, IntPtr[] certificates)
        {
            return SSLStreamCreateWithCertificates(
                ref MemoryMarshal.GetReference(pkcs8PrivateKey),
                pkcs8PrivateKey.Length,
                algorithm,
                certificates,
                certificates.Length);
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamInitialize")]
        private static extern int SSLStreamInitializeImpl(
            SafeSslHandle sslHandle,
            [MarshalAs(UnmanagedType.U1)] bool isServer,
            SSLReadCallback streamRead,
            SSLWriteCallback streamWrite,
            int appBufferSize);
        internal static void SSLStreamInitialize(
            SafeSslHandle sslHandle,
            bool isServer,
            SSLReadCallback streamRead,
            SSLWriteCallback streamWrite,
            int appBufferSize)
        {
            int ret = SSLStreamInitializeImpl(sslHandle, isServer, streamRead, streamWrite, appBufferSize);
            if (ret != SUCCESS)
                throw new SslException();
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamConfigureParameters")]
        private static extern int SSLStreamConfigureParametersImpl(
            SafeSslHandle sslHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string targetHost);
        internal static void SSLStreamConfigureParameters(
            SafeSslHandle sslHandle,
            string targetHost)
        {
            int ret = SSLStreamConfigureParametersImpl(sslHandle, targetHost);
            if (ret != SUCCESS)
                throw new SslException();
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamSetEnabledProtocols")]
        private static extern int SSLStreamSetEnabledProtocols(SafeSslHandle sslHandle, ref SslProtocols protocols, int length);
        internal static void SSLStreamSetEnabledProtocols(SafeSslHandle sslHandle, ReadOnlySpan<SslProtocols> protocols)
        {
            int ret = SSLStreamSetEnabledProtocols(sslHandle, ref MemoryMarshal.GetReference(protocols), protocols.Length);
            if (ret != SUCCESS)
                throw new SslException();
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamHandshake")]
        internal static extern PAL_SSLStreamStatus SSLStreamHandshake(SafeSslHandle sslHandle);

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamGetApplicationProtocol")]
        private static extern int SSLStreamGetApplicationProtocol(SafeSslHandle ssl, [Out] byte[]? buf, ref int len);
        internal static byte[]? SSLStreamGetApplicationProtocol(SafeSslHandle ssl)
        {
            int len = 0;
            int ret = SSLStreamGetApplicationProtocol(ssl, null, ref len);
            if (ret != INSUFFICIENT_BUFFER)
                return null;

            byte[] bytes = new byte[len];
            ret = SSLStreamGetApplicationProtocol(ssl, bytes, ref len);
            if (ret != SUCCESS)
                return null;

            return bytes;
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamRead")]
        private static unsafe extern PAL_SSLStreamStatus SSLStreamRead(
            SafeSslHandle sslHandle,
            byte* buffer,
            int length,
            out int bytesRead);
        internal static unsafe PAL_SSLStreamStatus SSLStreamRead(
            SafeSslHandle sslHandle,
            Span<byte> buffer,
            out int bytesRead)
        {
            fixed (byte* bufferPtr = buffer)
            {
                return SSLStreamRead(sslHandle, bufferPtr, buffer.Length, out bytesRead);
            }
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamWrite")]
        private static unsafe extern PAL_SSLStreamStatus SSLStreamWrite(
            SafeSslHandle sslHandle,
            byte* buffer,
            int length);
        internal static unsafe PAL_SSLStreamStatus SSLStreamWrite(
            SafeSslHandle sslHandle,
            ReadOnlyMemory<byte> buffer)
        {
            using (MemoryHandle memHandle = buffer.Pin())
            {
                return SSLStreamWrite(sslHandle, (byte*)memHandle.Pointer, buffer.Length);
            }
        }

        [DllImport(Interop.Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamRelease")]
        internal static extern void SSLStreamRelease(IntPtr ptr);

        internal sealed class SslException : Exception
        {
            internal SslException()
            {
            }

            internal SslException(int errorCode)
            {
                HResult = errorCode;
            }
        }

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamGetProtocol")]
        private static extern int SSLStreamGetProtocol(SafeSslHandle ssl, out IntPtr protocol);
        internal static string SSLStreamGetProtocol(SafeSslHandle ssl)
        {
            IntPtr protocolPtr;
            int ret = SSLStreamGetProtocol(ssl, out protocolPtr);
            if (ret != SUCCESS)
                throw new SslException();

            if (protocolPtr == IntPtr.Zero)
                return string.Empty;

            string protocol = Marshal.PtrToStringUni(protocolPtr)!;
            Marshal.FreeHGlobal(protocolPtr);
            return protocol;
        }

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamGetPeerCertificate")]
        internal static extern SafeX509Handle SSLStreamGetPeerCertificate(SafeSslHandle ssl);

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamGetPeerCertificates")]
        private static extern void SSLStreamGetPeerCertificates(
            SafeSslHandle ssl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] out IntPtr[] certs,
            out int count);
        internal static IntPtr[]? SSLStreamGetPeerCertificates(SafeSslHandle ssl)
        {
            IntPtr[]? ptrs;
            int count;
            Interop.AndroidCrypto.SSLStreamGetPeerCertificates(ssl, out ptrs, out count);
            return ptrs;
        }

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamGetCipherSuite")]
        private static extern int SSLStreamGetCipherSuite(SafeSslHandle ssl, out IntPtr cipherSuite);
        internal static string SSLStreamGetCipherSuite(SafeSslHandle ssl)
        {
            IntPtr cipherSuitePtr;
            int ret = SSLStreamGetCipherSuite(ssl, out cipherSuitePtr);
            if (ret != SUCCESS)
                throw new SslException();

            if (cipherSuitePtr == IntPtr.Zero)
                return string.Empty;

            string cipherSuite = Marshal.PtrToStringUni(cipherSuitePtr)!;
            Marshal.FreeHGlobal(cipherSuitePtr);
            return cipherSuite;
        }

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamShutdown")]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool SSLStreamShutdown(SafeSslHandle ssl);

        [DllImport(Libraries.CryptoNative, EntryPoint = "AndroidCryptoNative_SSLStreamVerifyHostname")]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool SSLStreamVerifyHostname(
            SafeSslHandle ssl,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string hostname);
    }
}

namespace System.Net
{
    internal sealed class SafeSslHandle : SafeHandle
    {
        public SafeSslHandle()
            : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Interop.AndroidCrypto.SSLStreamRelease(handle);
            SetHandle(IntPtr.Zero);
            return true;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
    }
}
