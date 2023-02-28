#define X86
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;
using WSNet;

namespace WSNet.SSPIProxy
{
    class SSPISession
    {
        public enum SEC_E : uint
        {
            OK = 0x00000000,
            CONTINUE_NEEDED = 0x00090312,
            INSUFFICIENT_MEMORY = 0x80090300,
            INTERNAL_ERROR = 0x80090304,
            INVALID_HANDLE = 0x80090301,
            INVALID_TOKEN = 0x80090308,
            LOGON_DENIED = 0x8009030C,
            NO_AUTHENTICATING_AUTHORITY = 0x80090311,
            NO_CREDENTIALS = 0x8009030E,
            TARGET_UNKNOWN = 0x80090303,
            UNSUPPORTED_FUNCTION = 0x80090302,
            WRONG_PRINCIPAL = 0x80090322,
            NOT_OWNER = 0x80090306,
            SECPKG_NOT_FOUND = 0x80090305,
            UNKNOWN_CREDENTIALS = 0x8009030D,
            RENEGOTIATE = 590625,
            COMPLETE_AND_CONTINUE = 590612,
            COMPLETE_NEEDED = 590611,
            INCOMPLETE_CREDENTIALS = 590624,
        }


        public enum SecBufferType
        {
            SECBUFFER_VERSION = 0,
            SECBUFFER_EMPTY = 0,
            SECBUFFER_DATA = 1,
            SECBUFFER_TOKEN = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecHandle //=PCtxtHandle
        {
            IntPtr dwLower; // ULONG_PTR translates to IntPtr not to uint
            IntPtr dwUpper; // this is crucial for 64-Bit Platforms
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecBuffer : IDisposable
        {
            public int cbBuffer;
            public int BufferType;
            public IntPtr pvBuffer;


            public SecBuffer(int bufferSize)
            {
                cbBuffer = bufferSize;
                BufferType = (int)SecBufferType.SECBUFFER_TOKEN;
                pvBuffer = Marshal.AllocHGlobal(bufferSize);
            }

            public SecBuffer(byte[] secBufferBytes)
            {
                cbBuffer = secBufferBytes.Length;
                BufferType = (int)SecBufferType.SECBUFFER_TOKEN;
                pvBuffer = Marshal.AllocHGlobal(cbBuffer);
                Marshal.Copy(secBufferBytes, 0, pvBuffer, cbBuffer);
            }

            public SecBuffer(byte[] secBufferBytes, SecBufferType bufferType)
            {
                cbBuffer = secBufferBytes.Length;
                BufferType = (int)bufferType;
                pvBuffer = Marshal.AllocHGlobal(cbBuffer);
                Marshal.Copy(secBufferBytes, 0, pvBuffer, cbBuffer);
            }

            public void Dispose()
            {
                if (pvBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pvBuffer);
                    pvBuffer = IntPtr.Zero;
                }
            }
        }

        public struct MultipleSecBufferHelper
        {
            public byte[] Buffer;
            public SecBufferType BufferType;

            public MultipleSecBufferHelper(byte[] buffer, SecBufferType bufferType)
            {
                if (buffer == null || buffer.Length == 0)
                {
                    throw new ArgumentException("buffer cannot be null or 0 length");
                }

                Buffer = buffer;
                BufferType = bufferType;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct SecBufferDesc : IDisposable
        {

            public int ulVersion;
            public int cBuffers;
            public IntPtr pBuffers; //Point to SecBuffer

            public SecBufferDesc(int bufferSize)
            {
                ulVersion = (int)SecBufferType.SECBUFFER_VERSION;
                cBuffers = 1;
                SecBuffer ThisSecBuffer = new SecBuffer(bufferSize);
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(ThisSecBuffer));
                Marshal.StructureToPtr(ThisSecBuffer, pBuffers, false);
            }

            public SecBufferDesc(byte[] secBufferBytes)
            {
                ulVersion = (int)SecBufferType.SECBUFFER_VERSION;
                cBuffers = 1;
                SecBuffer ThisSecBuffer = new SecBuffer(secBufferBytes);
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(ThisSecBuffer));
                Marshal.StructureToPtr(ThisSecBuffer, pBuffers, false);
            }

            public SecBufferDesc(MultipleSecBufferHelper[] secBufferBytesArray)
            {
                if (secBufferBytesArray == null || secBufferBytesArray.Length == 0)
                {
                    throw new ArgumentException("secBufferBytesArray cannot be null or 0 length");
                }

                ulVersion = (int)SecBufferType.SECBUFFER_VERSION;
                cBuffers = secBufferBytesArray.Length;

                //Allocate memory for SecBuffer Array....
                pBuffers = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SecBuffer)) * cBuffers);

                for (int Index = 0; Index < secBufferBytesArray.Length; Index++)
                {
                    //Super hack: Now allocate memory for the individual SecBuffers
                    //and just copy the bit values to the SecBuffer array!!!
                    SecBuffer ThisSecBuffer = new SecBuffer(secBufferBytesArray[Index].Buffer, secBufferBytesArray[Index].BufferType);

                    //We will write out bits in the following order:
                    //int cbBuffer;
                    //int BufferType;
                    //pvBuffer;
                    //Note that we won't be releasing the memory allocated by ThisSecBuffer until we
                    //are disposed...
                    int CurrentOffset = Index * Marshal.SizeOf(typeof(SecBuffer));
                    Marshal.WriteInt32(pBuffers, CurrentOffset, ThisSecBuffer.cbBuffer);
                    Marshal.WriteInt32(pBuffers, CurrentOffset + Marshal.SizeOf(ThisSecBuffer.cbBuffer), ThisSecBuffer.BufferType);
                    Marshal.WriteIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(ThisSecBuffer.cbBuffer) + Marshal.SizeOf(ThisSecBuffer.BufferType), ThisSecBuffer.pvBuffer);
                }
            }

            public void Dispose()
            {
                if (pBuffers != IntPtr.Zero)
                {
                    if (cBuffers == 1)
                    {
                        SecBuffer ThisSecBuffer = (SecBuffer)Marshal.PtrToStructure(pBuffers, typeof(SecBuffer));
                        ThisSecBuffer.Dispose();
                    }
                    else
                    {
                        for (int Index = 0; Index < cBuffers; Index++)
                        {
                            //The bits were written out the following order:
                            //int cbBuffer;
                            //int BufferType;
                            //pvBuffer;
                            //What we need to do here is to grab a hold of the pvBuffer allocate by the individual
                            //SecBuffer and release it...
                            int CurrentOffset = Index * Marshal.SizeOf(typeof(SecBuffer));
                            IntPtr SecBufferpvBuffer = Marshal.ReadIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(typeof(int)) + Marshal.SizeOf(typeof(int)));
                            Marshal.FreeHGlobal(SecBufferpvBuffer);
                        }
                    }

                    Marshal.FreeHGlobal(pBuffers);
                    pBuffers = IntPtr.Zero;
                }
            }

            public byte[] GetSecBufferByteArray()
            {
                byte[] Buffer = null;

                if (pBuffers == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Object has already been disposed!!!");
                }

                if (cBuffers == 1)
                {
                    SecBuffer ThisSecBuffer = (SecBuffer)Marshal.PtrToStructure(pBuffers, typeof(SecBuffer));

                    if (ThisSecBuffer.cbBuffer > 0)
                    {
                        Buffer = new byte[ThisSecBuffer.cbBuffer];
                        Marshal.Copy(ThisSecBuffer.pvBuffer, Buffer, 0, ThisSecBuffer.cbBuffer);
                    }
                }
                else
                {
                    int BytesToAllocate = 0;

                    for (int Index = 0; Index < cBuffers; Index++)
                    {
                        //The bits were written out the following order:
                        //int cbBuffer;
                        //int BufferType;
                        //pvBuffer;
                        //What we need to do here calculate the total number of bytes we need to copy...
                        int CurrentOffset = Index * Marshal.SizeOf(typeof(SecBuffer));
                        BytesToAllocate += Marshal.ReadInt32(pBuffers, CurrentOffset);
                    }

                    Buffer = new byte[BytesToAllocate];

                    for (int Index = 0, BufferIndex = 0; Index < cBuffers; Index++)
                    {
                        //The bits were written out the following order:
                        //int cbBuffer;
                        //int BufferType;
                        //pvBuffer;
                        //Now iterate over the individual buffers and put them together into a
                        //byte array...
                        int CurrentOffset = Index * Marshal.SizeOf(typeof(SecBuffer));
                        int BytesToCopy = Marshal.ReadInt32(pBuffers, CurrentOffset);
                        IntPtr SecBufferpvBuffer = Marshal.ReadIntPtr(pBuffers, CurrentOffset + Marshal.SizeOf(typeof(int)) + Marshal.SizeOf(typeof(int)));
                        Marshal.Copy(SecBufferpvBuffer, Buffer, BufferIndex, BytesToCopy);
                        BufferIndex += BytesToCopy;
                    }
                }

                return (Buffer);
            }

            /*public SecBuffer GetSecBuffer()
            {
                if(pBuffers == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Object has already been disposed!!!");
                }

                return((SecBuffer)Marshal.PtrToStructure(pBuffers,typeof(SecBuffer)));
            }*/
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_INTEGER
        {
            public uint LowPart;
            public int HighPart;
            public SECURITY_INTEGER(int dummy)
            {
                LowPart = 0;
                HighPart = 0;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_HANDLE
        {
            public IntPtr LowPart;
            public IntPtr HighPart;
            public SECURITY_HANDLE(int dummy)
            {
                LowPart = HighPart = IntPtr.Zero;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct SecPkgContext_Sizes
        {
            public uint cbMaxToken;
            public uint cbMaxSignature;
            public uint cbBlockSize;
            public uint cbSecurityTrailer;
        };

        static public string GetLastError()
        {
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SecPkgContext_SessionKey
        {
            public UInt32 SessionKeyLength;
            public IntPtr SessionKey;
        }


        [DllImport("secur32.dll", SetLastError = true)]
        static extern int AcquireCredentialsHandle(
        string pszPrincipal, //SEC_CHAR*
        string pszPackage, //SEC_CHAR* //"Kerberos","NTLM","Negotiative"
        int fCredentialUse,
        IntPtr PAuthenticationID,//_LUID AuthenticationID,//pvLogonID, //PLUID
        IntPtr pAuthData,//PVOID
        int pGetKeyFn, //SEC_GET_KEY_FN
        IntPtr pvGetKeyArgument, //PVOID
        ref SECURITY_HANDLE phCredential, //SecHandle //PCtxtHandle ref
        ref SECURITY_INTEGER ptsExpiry); //PTimeStamp //TimeStamp ref

        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int QueryContextAttributes(ref SECURITY_HANDLE phContext,
                                                    uint ulAttribute,
                                                    out SecPkgContext_Sizes pContextAttributes);

        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int QueryContextAttributes(ref SECURITY_HANDLE phContext,
                                                    uint ulAttribute,
                                                    out SecPkgContext_SessionKey pSessionKeyStruct);

#if X64
            [DllImport("secur32.dll", SetLastError = true)]
            static extern int InitializeSecurityContext(ref SECURITY_HANDLE phCredential,//PCredHandle
            IntPtr phContext, //PCtxtHandle
            string pszTargetName,
            ulong fContextReq,
            int Reserved1,
            int TargetDataRep,
            IntPtr pInput, //PSecBufferDesc SecBufferDesc
            int Reserved2,
            out SECURITY_HANDLE phNewContext, //PCtxtHandle
            out SecBufferDesc pOutput, //PSecBufferDesc SecBufferDesc
            out ulong pfContextAttr, //managed ulong == 64 bits!!!
            out SECURITY_INTEGER ptsExpiry); //PTimeStamp

            [DllImport("secur32.dll", SetLastError = true)]
            static extern int InitializeSecurityContext(ref SECURITY_HANDLE phCredential,//PCredHandle
            ref SECURITY_HANDLE phContext, //PCtxtHandle
            string pszTargetName,
            ulong fContextReq,
            int Reserved1,
            int TargetDataRep,
            ref SecBufferDesc pInput, //PSecBufferDesc SecBufferDesc
            int Reserved2,
            out SECURITY_HANDLE phNewContext, //PCtxtHandle
            out SecBufferDesc pOutput, //PSecBufferDesc SecBufferDesc
            out ulong pfContextAttr, //managed ulong == 64 bits!!!
            out SECURITY_INTEGER ptsExpiry); //PTimeStamp
#elif X86
        [DllImport("secur32.dll", SetLastError = true)]
        static extern int InitializeSecurityContext(ref SECURITY_HANDLE phCredential,//PCredHandle
        IntPtr phContext, //PCtxtHandle
        string pszTargetName,
        uint fContextReq,
        int Reserved1,
        int TargetDataRep,
        IntPtr pInput, //PSecBufferDesc SecBufferDesc
        int Reserved2,
        out SECURITY_HANDLE phNewContext, //PCtxtHandle
        out SecBufferDesc pOutput, //PSecBufferDesc SecBufferDesc
        out uint pfContextAttr, //managed ulong == 64 bits!!!
        out SECURITY_INTEGER ptsExpiry); //PTimeStamp

        [DllImport("secur32.dll", SetLastError = true)]
        static extern int InitializeSecurityContext(ref SECURITY_HANDLE phCredential,//PCredHandle
        ref SECURITY_HANDLE phContext, //PCtxtHandle
        string pszTargetName,
        uint fContextReq,
        int Reserved1,
        int TargetDataRep,
        ref SecBufferDesc pInput, //PSecBufferDesc SecBufferDesc
        int Reserved2,
        out SECURITY_HANDLE phNewContext, //PCtxtHandle
        out SecBufferDesc pOutput, //PSecBufferDesc SecBufferDesc
        out uint pfContextAttr, //managed ulong == 64 bits!!!
        out SECURITY_INTEGER ptsExpiry); //PTimeStamp
#endif
        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int EncryptMessage(ref SECURITY_HANDLE phContext,
                                            uint fQOP,        //managed ulong == 64 bits!!!
                                            ref SecBufferDesc pMessage,
                                            uint MessageSeqNo);    //managed ulong == 64 bits!!!

        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int DecryptMessage(ref SECURITY_HANDLE phContext,
                                                 ref SecBufferDesc pMessage,
                                                 uint MessageSeqNo,
                                                 out uint pfQOP);

        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int MakeSignature(ref SECURITY_HANDLE phContext,          // Context to use
                                            uint fQOP,         // Quality of Protection
                                            ref SecBufferDesc pMessage,        // Message to sign
                                            uint MessageSeqNo);      // Message Sequence Num.

        [DllImport("secur32.Dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int VerifySignature(ref SECURITY_HANDLE phContext,          // Context to use
                                                ref SecBufferDesc pMessage,        // Message to sign
                                                uint MessageSeqNo,            // Message Sequence Num.
                                                out uint pfQOP);      // Quality of Protection

        SECURITY_HANDLE phCredential = new SECURITY_HANDLE(0);
        SECURITY_HANDLE hClientContext = new SECURITY_HANDLE(0);
        SECURITY_INTEGER ptsExpiry = new SECURITY_INTEGER();
        const int MAX_TOKEN_SIZE = 12288;
        string auth_user_name = null;
        string target_name = null;
#if X64
            ulong ContextAttributes = 0; //flags
#elif X86
        uint ContextAttributes = 0; //flags
#endif
        int targetdatarep = 0;
        SecBufferDesc ClientToken = new SecBufferDesc(MAX_TOKEN_SIZE);
        public CancellationToken terminate_plugin_token;
        bool has_credctx = false;
        bool has_secctx = false;



        public Tuple<bool, byte[]> ntlmauth(string username, int credusage, string targetname, int contextattrs)
        {
            string pszPackage = "NTLM";

            IntPtr PAuthenticationID = IntPtr.Zero; //cmd.parameters[4];
            IntPtr pAuthData = IntPtr.Zero;
            int pGetKeyFn = 0;
            IntPtr pvGetKeyArgument = IntPtr.Zero;

            int res = AcquireCredentialsHandle(username, pszPackage, credusage, PAuthenticationID, pAuthData, pGetKeyFn, pvGetKeyArgument, ref phCredential, ref ptsExpiry);
            if (res != 0)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(res, errmsg);
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }

            res = InitializeSecurityContext(ref phCredential,
                IntPtr.Zero,
                null,// null string pszTargetName,
                (uint)contextattrs,
                0,//int Reserved1,
                targetdatarep,//int TargetDataRep
                IntPtr.Zero,    //Always zero first time around...
                0, //int Reserved2,
                out hClientContext, //pHandle CtxtHandle = SecHandle
                out ClientToken,//ref SecBufferDesc pOutput, //PSecBufferDesc
                out ContextAttributes,//ref int pfContextAttr,
                out ptsExpiry); //ref IntPtr ptsExpiry ); //PTimeStamp

            if (res != (uint)SEC_E.OK && res != (uint)SEC_E.COMPLETE_AND_CONTINUE && res != (uint)SEC_E.COMPLETE_NEEDED && res != (uint)SEC_E.CONTINUE_NEEDED && res != (uint)SEC_E.INCOMPLETE_CREDENTIALS)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(res, "InitializeSecurityContext: " + errmsg.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }
            byte[] authdata = ClientToken.GetSecBufferByteArray();

            CMDNTLMAuthReply reply = new CMDNTLMAuthReply(res, contextattrs, authdata);
            return new Tuple<bool, byte[]>(true, reply.to_bytes());
        }

        public Tuple<bool, byte[]> ntlmchallenge(int contextattrs, byte[] authdata, string targetname)
        {
            MultipleSecBufferHelper sb = new MultipleSecBufferHelper(authdata, SecBufferType.SECBUFFER_TOKEN);
            MultipleSecBufferHelper[] sbl = { sb };
            SecBufferDesc client_token = new SecBufferDesc(sbl);

            SecBufferDesc ClientTokenOut = new SecBufferDesc(MAX_TOKEN_SIZE);

            uint ContextAttributes = (uint)contextattrs;

            int res = InitializeSecurityContext(ref phCredential,
                ref hClientContext,
                targetname,// null string pszTargetName,
                ContextAttributes,
                0,//int Reserved1,
                targetdatarep,//int TargetDataRep
                ref client_token,    //Always zero first time around...
                0, //int Reserved2,
                out hClientContext, //pHandle CtxtHandle = SecHandle
                out ClientTokenOut,//ref SecBufferDesc pOutput, //PSecBufferDesc
                out ContextAttributes,//ref int pfContextAttr,
                out ptsExpiry); //ref IntPtr ptsExpiry ); //PTimeStamp

            if (res != (uint)SEC_E.OK && res != (uint)SEC_E.COMPLETE_AND_CONTINUE && res != (uint)SEC_E.COMPLETE_NEEDED && res != (uint)SEC_E.CONTINUE_NEEDED && res != (uint)SEC_E.INCOMPLETE_CREDENTIALS)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(res, "InitializeSecurityContext: " + errmsg.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }

            authdata = ClientTokenOut.GetSecBufferByteArray();
            CMDNTLMChallengeReply reply = new CMDNTLMChallengeReply(res, (int)ContextAttributes, authdata);
            return new Tuple<bool, byte[]>(true, reply.to_bytes());

        }

        public Tuple<bool, byte[]> kerberos(string username, int credusage, string targetname, int contextattrs, byte[] authdata)
        {
            try
            {
                SecBufferDesc client_token_in = new SecBufferDesc(MAX_TOKEN_SIZE);
                SecBufferDesc ClientTokenOut = new SecBufferDesc(MAX_TOKEN_SIZE);

                ContextAttributes = (uint)contextattrs;

                if (authdata != null)
                {
                    MultipleSecBufferHelper sb = new MultipleSecBufferHelper(authdata, SecBufferType.SECBUFFER_TOKEN);
                    MultipleSecBufferHelper[] sbl = { sb };
                    client_token_in = new SecBufferDesc(sbl);
                }




                string pszPackage = "Kerberos";

                IntPtr PAuthenticationID = IntPtr.Zero; //cmd.parameters[4];
                IntPtr pAuthData = IntPtr.Zero;
                int pGetKeyFn = 0;
                IntPtr pvGetKeyArgument = IntPtr.Zero;

                int res = 0;
                if (!has_credctx)
                {
                    res = AcquireCredentialsHandle(username, pszPackage, credusage, PAuthenticationID, pAuthData, pGetKeyFn, pvGetKeyArgument, ref phCredential, ref ptsExpiry);
                    if (res != 0)
                    {
                        string errmsg = GetLastError();
                        CMDAuthErr err = new CMDAuthErr(res, "AcquireCredentialsHandle (kerb): " + errmsg.ToString());

                        return new Tuple<bool, byte[]>(false, err.to_bytes());
                    }
                    has_credctx = true;

                }

                if (!has_secctx)
                {
                    res = InitializeSecurityContext(ref phCredential,
                        IntPtr.Zero,
                        targetname,// null string pszTargetName,
                        ContextAttributes,
                        0,//int Reserved1,
                        targetdatarep,//int TargetDataRep
                        IntPtr.Zero,    //Always zero first time around...
                        0, //int Reserved2,
                        out hClientContext, //pHandle CtxtHandle = SecHandle
                        out ClientToken,//ref SecBufferDesc pOutput, //PSecBufferDesc
                        out ContextAttributes,//ref int pfContextAttr,
                        out ptsExpiry
                     ); //ref IntPtr ptsExpiry ); //PTimeStamp

                    if (res != (uint)SEC_E.OK && res != (uint)SEC_E.COMPLETE_AND_CONTINUE && res != (uint)SEC_E.COMPLETE_NEEDED && res != (uint)SEC_E.CONTINUE_NEEDED && res != (uint)SEC_E.INCOMPLETE_CREDENTIALS)
                    {
                        string errmsg = GetLastError();
                        CMDAuthErr err = new CMDAuthErr(res, "InitializeSecurityContext (kerb): " + errmsg.ToString());
                        return new Tuple<bool, byte[]>(false, err.to_bytes());
                    }
                    has_secctx = true;
                }
                else
                {
                    res = InitializeSecurityContext(ref phCredential,
                    ref hClientContext,
                    target_name,// null string pszTargetName,
                    ContextAttributes,
                    0,//int Reserved1,
                    targetdatarep,//int TargetDataRep
                    ref client_token_in,    //Always zero first time around...
                    0, //int Reserved2,
                    out hClientContext, //pHandle CtxtHandle = SecHandle
                    out ClientToken,//ref SecBufferDesc pOutput, //PSecBufferDesc
                    out ContextAttributes,//ref int pfContextAttr,
                    out ptsExpiry); //ref IntPtr ptsExpiry ); //PTimeStamp

                    if (res != (uint)SEC_E.OK && res != (uint)SEC_E.COMPLETE_AND_CONTINUE && res != (uint)SEC_E.COMPLETE_NEEDED && res != (uint)SEC_E.CONTINUE_NEEDED && res != (uint)SEC_E.INCOMPLETE_CREDENTIALS)
                    {
                        string errmsg = GetLastError();
                        CMDAuthErr err = new CMDAuthErr(res, "InitializeSecurityContext2 (kerb): " + errmsg.ToString());
                        return new Tuple<bool, byte[]>(false, err.to_bytes());
                    }
                }


                byte[] out_authdata = ClientToken.GetSecBufferByteArray();

                CMDKerberosReply reply = new CMDKerberosReply(res, (int)ContextAttributes, out_authdata);
                return new Tuple<bool, byte[]>(true, reply.to_bytes());
            }
            catch (Exception e)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(-1, "Generic error -kerberos- " + e.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }

        }

        public Tuple<bool, byte[]> sessionkey()
        {
            try
            {
                SecPkgContext_SessionKey sessionkey_buff = new SecPkgContext_SessionKey();

                int res = QueryContextAttributes(ref hClientContext, 9, out sessionkey_buff);
                if (res != 0)
                {
                    string errmsg = GetLastError();
                    CMDAuthErr err = new CMDAuthErr(res, "QueryContextAttributes: " + errmsg.ToString());
                    return new Tuple<bool, byte[]>(false, err.to_bytes());
                }

                byte[] SessionKey = new byte[sessionkey_buff.SessionKeyLength];
                Marshal.Copy(sessionkey_buff.SessionKey, SessionKey, 0, (int)sessionkey_buff.SessionKeyLength);

                CMDSessionKeyReply reply = new CMDSessionKeyReply(res, SessionKey);
                return new Tuple<bool, byte[]>(true, reply.to_bytes());
            }
            catch (Exception e)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(-1, "Generic error -sessionkey- " + e.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }

        }

        public Tuple<bool, byte[]> sequenceno()
        {
            try
            {
                byte[] tokdata = new byte[1024];
                byte[] message = new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 };

                SecPkgContext_Sizes ContextSizes = new SecPkgContext_Sizes();

                if (QueryContextAttributes(ref hClientContext, 0, out ContextSizes) != 0)
                {
                    throw new Exception("QueryContextAttribute() failed!!!");
                }

                MultipleSecBufferHelper[] ThisSecHelper = new MultipleSecBufferHelper[2];
                ThisSecHelper[0] = new MultipleSecBufferHelper(message, SecBufferType.SECBUFFER_DATA);
                ThisSecHelper[1] = new MultipleSecBufferHelper(new byte[ContextSizes.cbSecurityTrailer], SecBufferType.SECBUFFER_TOKEN);

                SecBufferDesc DescBuffer = new SecBufferDesc(ThisSecHelper);

                int res = EncryptMessage(ref hClientContext, 0, ref DescBuffer, 0);
                if (res != 0)
                {
                    string errmsg = GetLastError();
                    CMDAuthErr err = new CMDAuthErr(res, "EncryptMessage: " + errmsg.ToString());
                    return new Tuple<bool, byte[]>(false, err.to_bytes());
                }

                byte[] encdata = DescBuffer.GetSecBufferByteArray();
                CMDSequenceReply reply = new CMDSequenceReply(res, encdata);

                return new Tuple<bool, byte[]>(true, reply.to_bytes());
            }
            catch (Exception e)
            {
                string errmsg = GetLastError();
                CMDAuthErr err = new CMDAuthErr(-1, "Generic error -sequenceno- " + e.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }


        }
    }
}
