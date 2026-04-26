using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;

namespace LimitedInvincible
{
    internal static class AiIgnoreInvincibleNativePatch
    {
        private const string GameAssemblyModuleName = "GameAssembly.dll";
        private const int PatchRva = 0x57F65B;
        private const int ValidationStartRva = 0x57F64B;
        private const int ValidationLength = 34;
        private const int SignatureLength = 34;
        private const int PatchOffsetFromSignature = 16;
        private const int PatchSize = 1;
        private const byte OriginalOpcode = 0x76;
        private const byte PatchedOpcode = 0xEB;
        private const uint PageExecuteReadWrite = 0x40;

        private static readonly byte[] ValidationBytes =
        {
            0x45, 0x33, 0xC0, 0x41, 0x8D, 0x50, 0x5F, 0xE8, 0x39, 0x44, 0x43, 0x00,
            0x41, 0x0F, 0x2F, 0xC2, 0x76, 0x14, 0x45, 0x33, 0xC0, 0x41, 0x0F, 0x28,
            0xC8, 0x41, 0x0F, 0x28, 0xC2, 0xE8, 0x33, 0x8E, 0xA9, 0x00
        };

        private static readonly bool[] ValidationMask =
        {
            true, true, true, true, true, true, true, true, true, true, true, true,
            true, true, true, true, false, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true, true, true
        };

        private static readonly byte[] SignatureBytes =
        {
            0x45, 0x33, 0xC0, 0x41, 0x8D, 0x50, 0x5F, 0xE8, 0x00, 0x00, 0x00, 0x00,
            0x41, 0x0F, 0x2F, 0xC2, 0x76, 0x14, 0x45, 0x33, 0xC0, 0x41, 0x0F, 0x28,
            0xC8, 0x41, 0x0F, 0x28, 0xC2, 0xE8, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly bool[] SignatureMask =
        {
            true, true, true, true, true, true, true, true, false, false, false, false,
            true, true, true, true, true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, false, false, false, false
        };

        private static bool _isInstalled;
        private static byte[] _backupBytes;

        internal static void TryInstall(MelonLogger.Instance log)
        {
            if (_isInstalled || !Main.EnableAiIgnoreInvincible)
            {
                return;
            }

            try
            {
                ProcessModule module = GetGameAssemblyModule();
                if (module == null)
                {
                    log.Warning("未找到 GameAssembly.dll，无法安装 AI 无视无敌原生补丁。");
                    return;
                }

                IntPtr patchAddress = FindPatchAddress(module, log);
                if (patchAddress == IntPtr.Zero)
                {
                    return;
                }

                byte[] currentBytes = ReadBytes(patchAddress, PatchSize);
                if (currentBytes[0] == PatchedOpcode)
                {
                    _backupBytes = new[] { OriginalOpcode };
                    _isInstalled = true;
                    log.Msg("检测到 AI 无视无敌原生补丁已生效。");
                    return;
                }

                if (currentBytes[0] != OriginalOpcode)
                {
                    log.Warning($"AI 无视无敌补丁点字节不匹配，跳过原生补丁。当前字节：{BitConverter.ToString(currentBytes)}");
                    return;
                }

                _backupBytes = currentBytes;
                WriteBytes(patchAddress, new[] { PatchedOpcode });
                _isInstalled = true;
                log.Msg("已安装 AI 无视无敌原生补丁，仅忽略技能规划中的目标无敌分支。");
            }
            catch (Exception ex)
            {
                log.Warning($"安装 AI 无视无敌原生补丁失败：{ex.Message}");
            }
        }

        internal static void TryRestore(MelonLogger.Instance log)
        {
            if (!_isInstalled || _backupBytes == null)
            {
                return;
            }

            try
            {
                ProcessModule module = GetGameAssemblyModule();
                if (module == null)
                {
                    return;
                }

                IntPtr patchAddress = FindPatchAddress(module, log);
                if (patchAddress == IntPtr.Zero)
                {
                    return;
                }

                WriteBytes(patchAddress, _backupBytes);
                _isInstalled = false;
                log.Msg("已恢复 AI 无视无敌原生补丁。");
            }
            catch (Exception ex)
            {
                log.Warning($"恢复 AI 无视无敌原生补丁失败：{ex.Message}");
            }
        }

        private static IntPtr FindPatchAddress(ProcessModule module, MelonLogger.Instance log)
        {
            IntPtr fixedAddress = TryGetFixedPatchAddress(module, log);
            if (fixedAddress != IntPtr.Zero)
            {
                return fixedAddress;
            }

            byte[] moduleBytes = ReadBytes(module.BaseAddress, module.ModuleMemorySize);
            int matchOffset = FindSignatureOffset(moduleBytes);
            if (matchOffset < 0)
            {
                log.Warning("未找到 AI 无视无敌特征码，跳过原生补丁。");
                return IntPtr.Zero;
            }

            if (Main.VerboseLogEnabled)
            {
                log.Msg($"AI 无视无敌特征码命中偏移：0x{matchOffset:X}。");
            }

            return IntPtr.Add(module.BaseAddress, matchOffset + PatchOffsetFromSignature);
        }

        private static IntPtr TryGetFixedPatchAddress(ProcessModule module, MelonLogger.Instance log)
        {
            if (module.ModuleMemorySize < ValidationStartRva + ValidationLength)
            {
                return IntPtr.Zero;
            }

            IntPtr validationAddress = IntPtr.Add(module.BaseAddress, ValidationStartRva);
            byte[] currentBytes = ReadBytes(validationAddress, ValidationLength);
            if (!BytesMatch(currentBytes, ValidationBytes, ValidationMask))
            {
                if (Main.VerboseLogEnabled)
                {
                    log.Msg($"AI 无视无敌固定补丁点校验失败，回退到特征码扫描。当前字节：{BitConverter.ToString(currentBytes)}");
                }

                return IntPtr.Zero;
            }

            if (Main.VerboseLogEnabled)
            {
                log.Msg($"AI 无视无敌固定补丁点命中 RVA：0x{PatchRva:X}。");
            }

            return IntPtr.Add(module.BaseAddress, PatchRva);
        }

        private static int FindSignatureOffset(byte[] buffer)
        {
            int maxOffset = buffer.Length - SignatureLength;
            for (int offset = 0; offset <= maxOffset; offset++)
            {
                bool matched = true;
                for (int i = 0; i < SignatureLength; i++)
                {
                    if (SignatureMask[i] && buffer[offset + i] != SignatureBytes[i])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return offset;
                }
            }

            return -1;
        }

        private static bool BytesMatch(byte[] current, byte[] expected, bool[] mask)
        {
            if (current.Length != expected.Length || expected.Length != mask.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (mask[i] && current[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] ReadBytes(IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            Marshal.Copy(address, buffer, 0, size);
            return buffer;
        }

        private static void WriteBytes(IntPtr address, byte[] bytes)
        {
            if (!VirtualProtect(address, (UIntPtr)bytes.Length, PageExecuteReadWrite, out uint oldProtect))
            {
                throw new InvalidOperationException("VirtualProtect 失败。");
            }

            try
            {
                Marshal.Copy(bytes, 0, address, bytes.Length);
                FlushInstructionCache(Process.GetCurrentProcess().Handle, address, (UIntPtr)bytes.Length);
            }
            finally
            {
                VirtualProtect(address, (UIntPtr)bytes.Length, oldProtect, out _);
            }
        }

        private static ProcessModule GetGameAssemblyModule()
        {
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                if (string.Equals(module.ModuleName, GameAssemblyModuleName, StringComparison.OrdinalIgnoreCase))
                {
                    return module;
                }
            }

            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint flNewProtect,
            out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            UIntPtr dwSize);
    }
}
