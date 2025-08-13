using GdprConfigurations;
using System.Security.Cryptography;

namespace UnitTests.Services
{
    [TestFixture]
    public class EncryptionProviderTests
    {
        [Test]
        public void DecryptString_TamperedData_ThrowsCryptographicException()
        {
            byte[] key = new byte[32];
            RandomNumberGenerator.Fill(key);
            string plaintext = "test@example.com";
            byte[] encrypted = EncryptionProvider.EncryptString(plaintext, key);
            encrypted[0] ^= 0xFF; // Tamper with nonce
            Assert.Throws<CryptographicException>(() => EncryptionProvider.DecryptString(encrypted, key));
        }
    }
}
