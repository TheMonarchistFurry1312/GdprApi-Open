using System.Security.Cryptography;
using System.Text;

namespace GdprConfigurations
{
    public static class EncryptionProvider
    {
        public static string HashString(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input), "Input cannot be null or empty.");

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static byte[] EncryptString(string plaintext, byte[] key)
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new ArgumentNullException(nameof(plaintext), "Plaintext cannot be null or empty.");
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes for AES-256-GCM.", nameof(key));

            byte[] nonce = new byte[12]; // AES-GCM recommends 12-byte nonce
            RandomNumberGenerator.Fill(nonce);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[16]; // AES-GCM produces 16-byte authentication tag

            using (var aesGcm = new AesGcm(key))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            // Combine nonce, tag, and ciphertext: [nonce (12 bytes) | tag (16 bytes) | ciphertext]
            byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }

        public static string DecryptString(byte[] encryptedData, byte[] key)
        {
            if (encryptedData == null || encryptedData.Length < 28) // Minimum: 12-byte nonce + 16-byte tag
                throw new ArgumentNullException(nameof(encryptedData), "Encrypted data is null or too short.");
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes for AES-256-GCM.", nameof(key));

            // Extract nonce (12 bytes), tag (16 bytes), and ciphertext
            byte[] nonce = encryptedData.Take(12).ToArray();
            byte[] tag = encryptedData.Skip(12).Take(16).ToArray();
            byte[] ciphertext = encryptedData.Skip(12 + 16).ToArray();

            byte[] plaintextBytes = new byte[ciphertext.Length];
            try
            {
                using (var aesGcm = new AesGcm(key))
                {
                    aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
                }
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("Decryption failed. Data may have been tampered with.", ex);
            }
        }
    }
}