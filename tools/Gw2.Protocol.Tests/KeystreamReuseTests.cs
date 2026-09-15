using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class KeystreamReuseTests
    {
        [TestMethod]
        public void OutboundState_Advanced_ReproducesInboundState()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("keystream-reuse.json"));
            var root = doc.RootElement;
            var ob = root.GetProperty("outboundState");
            var ib = root.GetProperty("inboundState");
            int advance = root.GetProperty("outboundToInboundAdvance").GetInt32();

            var outbound = TransportCipherState.FromCapturedState(
                Convert.ToInt32(ob.GetProperty("i").GetString(), 16),
                Convert.ToInt32(ob.GetProperty("j").GetString(), 16),
                Convert.FromHexString(ob.GetProperty("sboxHex").GetString()));
            var inbound = TransportCipherState.FromCapturedState(
                Convert.ToInt32(ib.GetProperty("i").GetString(), 16),
                Convert.ToInt32(ib.GetProperty("j").GetString(), 16),
                Convert.FromHexString(ib.GetProperty("sboxHex").GetString()));

            // Advancing the outbound PRGA reproduces the inbound state: same keystream.
            TransportCipher.Crypt(outbound, new byte[advance], new byte[advance]);

            Assert.AreEqual(inbound.I, outbound.I);
            Assert.AreEqual(inbound.J, outbound.J);
            Assert.AreEqual(Convert.ToHexString(inbound.CopySBox()),
                            Convert.ToHexString(outbound.CopySBox()));
        }
    }
}
