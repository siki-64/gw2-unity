using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    // Sanitized per-build fixtures (captured wire, cipher vectors, schema corpora) are external
    // evidence copied into the output directory. They are not present for every build; a missing
    // fixture is reported inconclusive so the harness stays runnable, never a silent pass.
    internal static class Fixtures
    {
        private static string Dir => Path.Combine(AppContext.BaseDirectory, "fixtures");

        internal static bool Present(string name) => File.Exists(Path.Combine(Dir, name));

        internal static byte[] ReadBytes(string name)
        {
            if (!Present(name)) Assert.Inconclusive("Fixture '" + name + "' is not present for the current build.");
            return File.ReadAllBytes(Path.Combine(Dir, name));
        }

        internal static string ReadText(string name)
        {
            if (!Present(name)) Assert.Inconclusive("Fixture '" + name + "' is not present for the current build.");
            return File.ReadAllText(Path.Combine(Dir, name));
        }
    }
}
