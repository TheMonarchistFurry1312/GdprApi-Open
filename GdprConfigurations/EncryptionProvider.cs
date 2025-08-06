using System.Security.Cryptography;
using System.Text;

namespace GdprConfigurations
{
    public static class EncryptionProvider
    {
        /// <summary>
        /// Decrypts a byte array to retrieve the original string.
        /// </summary>
        /// <param name="encryptedData">The encrypted data including IV.</param>
        /// <returns>The decrypted original string.</returns>
        public static string DecryptString(byte[] encryptedData, byte[] encryptionKey)
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            var iv = encryptedData.Take(16).ToArray(); // Extract IV (16 bytes for AES)
            var data = encryptedData.Skip(16).ToArray();
            var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(data);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Encrypts a string using AES for secure storage of original values.
        /// </summary>
        /// <param name="text">The text to encrypt.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public static byte[] EncryptString(string text, byte[] encryptionKey)
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.GenerateIV();
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV for decryption
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(text);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Generates a SHA-256 hash of the input string for pseudonymization.
        /// </summary>
        /// <param name="input">The string to hash (e.g., email, full name).</param>
        /// <returns>The base64-encoded hash of the input string.</returns>
        public static string HashString(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
