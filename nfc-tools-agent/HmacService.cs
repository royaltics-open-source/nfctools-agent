using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NFCToolsAgent
{
    internal class HmacService
    {

        // uidHex: "04112233..." (sin 0x, hex uppercase o lowercase)
        // masterKey: la misma cadena que usas en backend (se interpreta como UTF-8 bytes)
        // Resultado: array de 6 bytes (KeyA). Para hex: ToHex(keyA)
        public static string DeriveKeyA(string uidHex, string masterKey, string label)
        {
            if (string.IsNullOrWhiteSpace(uidHex)) throw new ArgumentException(nameof(uidHex));
            if (string.IsNullOrEmpty(masterKey)) throw new ArgumentException(nameof(masterKey));

            byte[] uidBytes = HexToBytes(uidHex);
            byte[] masterKeyBytes = Encoding.UTF8.GetBytes(masterKey); // debe coincidir con Buffer.from(...,'utf8') en Node
            byte[] labelBytes = Encoding.UTF8.GetBytes(label);

            byte[] message = new byte[labelBytes.Length + uidBytes.Length];
            Buffer.BlockCopy(labelBytes, 0, message, 0, labelBytes.Length);
            Buffer.BlockCopy(uidBytes, 0, message, labelBytes.Length, uidBytes.Length);

            var hmac = new HMACSHA256(masterKeyBytes);
            byte[] digest = hmac.ComputeHash(message); // 32 bytes
            return ToHex(digest.Take(6).ToArray()); // KeyA: primeros 6 bytes
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Trim();
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex must have even length", nameof(hex));
            int len = hex.Length / 2;
            var bytes = new byte[len];
            for (int i = 0; i < len; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        public static string ToHex(byte[] data) =>
            BitConverter.ToString(data).Replace("-", "").ToUpperInvariant();
    }

}