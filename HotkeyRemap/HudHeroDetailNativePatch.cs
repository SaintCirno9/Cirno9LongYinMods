using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;

namespace HotkeyRemap
{
    internal static class HudHeroDetailNativePatch
    {
        private const string GameAssemblyModuleName = "GameAssembly.dll";
        private const int SignatureLength = 18;
        private const int PatchOffsetFromSignature = 10;
        private const int PatchSize = 8;
        private const uint PageExecuteReadWrite = 0x40;

        private static readonly byte[] SignatureBytes =
        {
            0x33, 0xD2, 0x8D, 0x4A, 0x71, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x84, 0xC0, 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly bool[] SignatureMask =
        {
            true, true, true, true, true, true, false, false, false, false, true, true, true, true, false, false, false, false
        };

        private static bool _isInstalled;
        private static byte[] _backupBytes;

        internal static bool IsOriginalQDisabled => _isInstalled;

        internal static void TryInstall(MelonLogger.Instance log)
        {
            if (_isInstalled || !Main.Config.EnableGlobalRemap || Main.Config.HeroDetail == UnityEngine.KeyCode.Q)
            {
                return;
            }

            try
            {
                ProcessModule module = GetGameAssemblyModule();
                if (module == null)
                {
                    log.Warning("未找到 GameAssembly.dll，无法禁用原版人物详情 Q 键。");
                    return;
                }

                IntPtr patchAddress = FindPatchAddress(module, log);
                if (patchAddress == IntPtr.Zero)
                {
                    return;
                }

                byte[] currentBytes = ReadBytes(patchAddress, PatchSize);
                byte[] jumpBytes = BuildJumpPatch(currentBytes);
                if (BytesEqual(currentBytes, jumpBytes))
                {
                    _backupBytes = currentBytes;
                    _isInstalled = true;
                    log.Msg("检测到原版人物详情 Q 键已被禁用补丁接管。");
                    return;
                }

                if (currentBytes[0] != 0x84 || currentBytes[1] != 0xC0 || currentBytes[2] != 0x0F || currentBytes[3] != 0x84)
                {
                    log.Warning(
                        $"人物详情 Q 键补丁点字节不匹配，跳过原生补丁。当前字节：{BitConverter.ToString(currentBytes)}");
                    return;
                }

                _backupBytes = currentBytes;
                WriteBytes(patchAddress, jumpBytes);
                _isInstalled = true;
                log.Msg("已禁用原版人物详情 Q 键分支，改由配置热键接管。");
            }
            catch (Exception ex)
            {
                log.Warning($"安装人物详情 Q 键原生补丁失败：{ex.Message}");
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
                log.Msg("已恢复原版人物详情 Q 键分支。");
            }
            catch (Exception ex)
            {
                log.Warning($"恢复人物详情 Q 键原生补丁失败：{ex.Message}");
            }
        }

        private static byte[] ReadBytes(IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            Marshal.Copy(address, buffer, 0, size);
            return buffer;
        }

        private static IntPtr FindPatchAddress(ProcessModule module, MelonLogger.Instance log)
        {
            byte[] moduleBytes = ReadBytes(module.BaseAddress, module.ModuleMemorySize);
            int matchOffset = FindSignatureOffset(moduleBytes);
                if (matchOffset < 0)
                {
                    log.Warning("未找到人物详情 Q 键特征码，跳过原生补丁。");
                    return IntPtr.Zero;
                }

                if (Main.Config.VerboseLog)
                {
                    log.Msg($"人物详情 Q 键特征码命中偏移：0x{matchOffset:X}。");
                }

                return IntPtr.Add(module.BaseAddress, matchOffset + PatchOffsetFromSignature);
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

        private static byte[] BuildJumpPatch(byte[] originalBytes)
        {
            int relativeTarget = BitConverter.ToInt32(originalBytes, 4);
            int finalTargetFromPatchStart = 8 + relativeTarget;
            int jumpRelative = finalTargetFromPatchStart - 5;
            byte[] jumpBytes = { 0xE9, 0x00, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90 };
            Array.Copy(BitConverter.GetBytes(jumpRelative), 0, jumpBytes, 1, 4);
            return jumpBytes;
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

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
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
