using System;
using System.Linq;
using System.Text;
using PCSC;
using PCSC.Monitoring;

namespace NFCToolsAgent
{
    public class NfcService : IDisposable
    {
        private ISCardContext ctx;
        private ICardReader reader;
        private string readerName;
        private SCardMonitor monitor;

        private static readonly byte[] FactoryKey = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        public event Action<string> StatusChanged;
        public event Action<string> DebugLog;
        public event Action<string> CardInserted;
        public event Action CardRemoved;

        public NfcService()
        {
            InitContextAndMonitor();
        }

        public void Dispose()
        {
            try
            {
                monitor?.Cancel();
                monitor?.Dispose();
                reader?.Dispose();
                ctx?.Release();
            }
            catch { }
        }

        public bool IsReaderAvailable()
        {
            try
            {
                if (ctx == null) return false;
                if (readerName == null) return false;
                var readers = ctx.GetReaders();
                return readers != null && readers.Contains(readerName);
            }
            catch { return false; }
        }

        private void InitContextAndMonitor()
        {
            try
            {
                var factory = ContextFactory.Instance;
                ctx = factory.Establish(SCardScope.System);
                var readers = ctx.GetReaders();

                if (readers == null || readers.Length == 0)
                {
                    StatusChanged?.Invoke("No se detectó lector");
                    DebugLog?.Invoke("No readers available");
                    return;
                }

                readerName = readers[0];
                StatusChanged?.Invoke($"Reader: {readerName}. Esperando tarjeta...");
                DebugLog?.Invoke($"Using reader: {readerName}");

                monitor = new SCardMonitor(factory, SCardScope.System);

                monitor.CardInserted += (s, a) =>
                {
                    DebugLog?.Invoke("Card inserted event");
                    StatusChanged?.Invoke("Tarjeta insertada - conectando...");
                    ConnectReader();
                    try
                    {
                        string uid = ReadUID(reader);
                        DebugLog?.Invoke($"Tarjeta detectada. UID: {uid}");
                        StatusChanged?.Invoke($"Tarjeta presente - UID: {uid}");
                        CardInserted?.Invoke(uid);
                    }
                    catch (Exception ex)
                    {
                        DebugLog?.Invoke($"Error al leer UID: {ex.Message}");
                        StatusChanged?.Invoke("Error al conectar tarjeta");
                    }
                };

                monitor.CardRemoved += (s, a) =>
                {
                    DebugLog?.Invoke("Card removed event");
                    StatusChanged?.Invoke("Tarjeta removida");
                    CardRemoved?.Invoke();

                    try { reader?.Dispose(); reader = null; }
                    catch (Exception ex) { DebugLog?.Invoke($"Error liberando reader: {ex.Message}"); }
                };

                monitor.Start(readerName);
                DebugLog?.Invoke("Monitor started, listening for cards...");

                // Conectar automáticamente al lector al iniciar
                ConnectReader();
            }
            catch (Exception ex)
            {
                DebugLog?.Invoke($"Init error: {ex.Message}");
                StatusChanged?.Invoke("Error inicializando monitor");
            }
        }

        private void ConnectReader()
        {
            if (reader != null) return;

            try
            {
                reader = ctx.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
                DebugLog?.Invoke("Reader conectado exitosamente");
            }
            catch (Exception ex)
            {
                DebugLog?.Invoke($"No se pudo conectar al reader: {ex.Message}");
            }
        }

        private void EnsureReaderConnected()
        {
            if (reader == null) ConnectReader();
            if (reader == null) throw new Exception("No hay tarjeta/reader conectado");
        }

        public string GetUid()
        {
            EnsureReaderConnected();
            return ReadUID(reader);
        }

        public byte[] ReadSector(int sector, string authKeyText)
        {
            EnsureReaderConnected();
            var key = ParseHex(authKeyText);
            int block = sector * 4;
            int trailer = block + 3;
            return ReadBlockClassic1K(reader, block, trailer, key);
        }

        public void WriteSector(int sector, string authKeyText, string payloadText)
        {
            EnsureReaderConnected();
            var key = ParseHex(authKeyText);
            var payload = ParsePayloadString(payloadText);
            WriteBlockSector(reader, sector, key, payload);
        }

        public void ChangeKeys(int sector, string authKeyText, string keyAText, string keyBText, int accessOption)
        {
            EnsureReaderConnected();
            var authKey = ParseHex(authKeyText);
            var keyA = ParseHex(keyAText);
            var keyB = ParseHex(keyBText);
            ChangeKeyFinal(reader, sector, authKey, keyA, keyB, accessOption);
        }

        public void ResetSector(int sector, string authKeyText)
        {
            EnsureReaderConnected();
            var authKey = ParseHex(authKeyText);
            ResetSector(reader, sector, authKey);
        }

        // --------------------
        // Métodos internos NFC
        // --------------------
        private string ReadUID(ICardReader r)
        {
            byte[] apdu = { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
            var recv = new byte[258];
            int rcv = r.Transmit(apdu, recv);
            if (rcv < 2) throw new Exception("UID read failed");
            return BitConverter.ToString(recv, 0, rcv - 2).Replace("-", "");
        }

        private bool LoadKey(ICardReader r, byte[] key)
        {
            byte[] apdu = new byte[11];
            apdu[0] = 0xFF; apdu[1] = 0x82; apdu[2] = 0x00; apdu[3] = 0x00; apdu[4] = 0x06;
            Array.Copy(key, 0, apdu, 5, 6);
            var recv = new byte[258];
            int rcv = r.Transmit(apdu, recv);
            return rcv >= 2 && recv[rcv - 2] == 0x90 && recv[rcv - 1] == 0x00;
        }

        private bool Authenticate(ICardReader r, int block, byte[] key)
        {
            if (!LoadKey(r, key)) return false;
            byte[] apdu = { 0xFF, 0x86, 0x00, 0x00, 0x05, 0x01, 0x00, (byte)block, 0x60, 0x00 };
            var recv = new byte[258];
            int rcv = r.Transmit(apdu, recv);
            return rcv >= 2 && recv[rcv - 2] == 0x90 && recv[rcv - 1] == 0x00;
        }

        private byte[] ReadBlock(ICardReader r, int block)
        {
            byte[] apdu = { 0xFF, 0xB0, 0x00, (byte)block, 0x10 };
            var recv = new byte[258];
            int rcv = r.Transmit(apdu, recv);
            if (rcv < 2) return new byte[0];
            return recv.Take(rcv - 2).ToArray();
        }

        private byte[] ReadBlockClassic1K(ICardReader r, int blockToRead, int blockToAuth, byte[] key)
        {
            if (!Authenticate(r, blockToAuth, key)) throw new Exception("Autenticación falló");
            return ReadBlock(r, blockToRead);
        }

        private void WriteBlock(ICardReader r, int block, byte[] payload)
        {
            byte[] apdu = new byte[21];
            apdu[0] = 0xFF; apdu[1] = 0xD6; apdu[2] = 0x00; apdu[3] = (byte)block; apdu[4] = 0x10;
            Array.Copy(payload, 0, apdu, 5, 16);
            var recv = new byte[258];
            int rcv = r.Transmit(apdu, recv);
            if (!(rcv >= 2 && recv[rcv - 2] == 0x90 && recv[rcv - 1] == 0x00))
                throw new Exception("Error escribiendo bloque");
        }

        private void WriteBlockSector(ICardReader r, int sector, byte[] key, byte[] payload)
        {
            int block = sector * 4;
            int trailer = block + 3;
            if (!Authenticate(r, trailer, key)) throw new Exception("Autenticación falló");
            WriteBlock(r, block, payload);
        }

        private void ChangeKeyFinal(ICardReader r, int sector, byte[] authKey, byte[] newKeyA, byte[] newKeyB, int accessOption)
        {
            int trailer = sector * 4 + 3;
            if (!Authenticate(r, trailer, authKey)) throw new Exception("No se pudo autenticar");

            byte[] payload = new byte[16];
            Array.Copy(newKeyA, 0, payload, 0, 6);
            Array.Copy(BuildAccessBits(accessOption), 0, payload, 6, 4);
            Array.Copy(newKeyB, 0, payload, 10, 6);
            WriteBlock(r, trailer, payload);
        }

        private void ResetSector(ICardReader r, int sector, byte[] authKey)
        {
            int trailer = sector * 4 + 3;
            if (!Authenticate(r, trailer, authKey)) throw new Exception("No se pudo autenticar");
            byte[] payload = new byte[16];
            Array.Copy(FactoryKey, 0, payload, 0, 6);
            Array.Copy(BuildAccessBits(0), 0, payload, 6, 4);
            Array.Copy(FactoryKey, 0, payload, 10, 6);
            WriteBlock(r, trailer, payload);
        }



        // --------------------
        // Helpers estáticos
        // --------------------




        private byte[] BuildAccessBits(int option)
        {
            int[,] cfg = new int[4, 3];
            cfg[0, 0] = (option >> 2) & 1; cfg[0, 1] = (option >> 1) & 1; cfg[0, 2] = option & 1;
            for (int i = 1; i <= 2; i++) for (int j = 0; j < 3; j++) cfg[i, j] = cfg[0, j];
            cfg[3, 0] = 0; cfg[3, 1] = 0; cfg[3, 2] = 0;

            byte c1 = (byte)((cfg[3, 0] << 3) | (cfg[2, 0] << 2) | (cfg[1, 0] << 1) | cfg[0, 0]);
            byte c2 = (byte)((cfg[3, 1] << 3) | (cfg[2, 1] << 2) | (cfg[1, 1] << 1) | cfg[0, 1]);
            byte c3 = (byte)((cfg[3, 2] << 3) | (cfg[2, 2] << 2) | (cfg[1, 2] << 1) | cfg[0, 2]);
            byte byte6 = (byte)(((~c2) & 0x0F) << 4 | ((~c1) & 0x0F));
            byte byte7 = (byte)((c1 << 4) | ((~c3) & 0x0F));
            byte byte8 = (byte)((c3 << 4) | (c2 & 0x0F));
            byte byte9 = (byte)((((~byte6) & 0xF0) >> 4) | (((~byte7) & 0x0F) << 4));
            return new byte[] { byte6, byte7, byte8, byte9 };
        }

        private static byte[] ParseHex(string hex)
        {
            hex = hex.Replace(":", "").Replace(" ", "");
            if (hex.Length % 2 != 0) throw new Exception("Hex inválido");
            if (hex.Length != 12) throw new Exception("Hex key debe tener 12 caracteres (6 bytes)");
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                             .ToArray();
        }


        private static byte[] ParsePayloadString(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "";
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length > 16) throw new Exception("El texto es demasiado largo, máximo 16 bytes UTF-8");
            if (bytes.Length < 16) { byte[] padded = new byte[16]; Array.Copy(bytes, padded, bytes.Length); return padded; }
            return bytes;
        }
    }
}
