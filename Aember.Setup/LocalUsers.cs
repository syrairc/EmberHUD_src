using System.Runtime.InteropServices;

namespace Aember.Setup;

internal static partial class LocalUsers
{
    [StructLayout(LayoutKind.Sequential)]
    struct UserInfo1
    {
        public nint Name;
        public nint Password;
        public int PasswordAge;
        public int Privilege;
        public nint HomeDirectory;
        public nint Comment;
        public int Flags;
        public nint ScriptPath;
    }

    [LibraryImport("netapi32.dll")]
    private static partial int NetUserEnum(nint server, int level, int filter, out nint buffer, int maxLength, out int read, out int total, ref int resume);

    [LibraryImport("netapi32.dll")]
    private static partial int NetApiBufferFree(nint buffer);

    public static IReadOnlyList<string> List()
    {
        List<string> users = [];
        var resume = 0;
        int status;
        do
        {
            status = NetUserEnum(0, 1, 2, out var buffer, -1, out var read, out _, ref resume);
            if (status is not (0 or 234)) break;
            try
            {
                for (var i = 0; i < read; i++)
                {
                    var user = Marshal.PtrToStructure<UserInfo1>(buffer + i * Marshal.SizeOf<UserInfo1>());
                    if ((user.Flags & 0x2) == 0 && Marshal.PtrToStringUni(user.Name) is { } name && !name.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase)) users.Add(name);
                }
            }
            finally
            {
                NetApiBufferFree(buffer);
            }
        }
        while (status == 234);
        return [.. users.Order(StringComparer.OrdinalIgnoreCase)];
    }
}
