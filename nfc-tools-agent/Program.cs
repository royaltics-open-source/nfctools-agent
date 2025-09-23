using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Linq;

namespace NFCToolsAgent
{

    static class Program
    {


        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nfc_agent.log");

        private static NfcService nfc;
        private static int PORT = 1616;
        private static byte[] secretRunKey;
        private static HttpListener listener;
        private static NotifyIcon trayIcon;
        private static ToolStripMenuItem portMenuItem;



        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InitLogging();

            nfc = new NfcService();
            nfc.DebugLog += msg => WriteLog($"[DEBUG] {msg}");
            nfc.StatusChanged += status => WriteLog($"[ESTATUS] {status}");
            secretRunKey = GenerateSecretKey();

            // Inicializa TrayIcon
            InitTrayIcon();

            // Inicializa HTTPListener
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{PORT}/pcsc/");
            try
            {
                listener.Start();
                portMenuItem.Text = $"PORT {PORT}: ON";
            }
            catch
            {
                portMenuItem.Text = $"PORT {PORT}: OFF";
            }

            ThreadPool.QueueUserWorkItem(o => ListenLoop());
            WriteLog($"[DEBUG] NFC Agent Tool, Listo y escuchando");
            trayIcon.ShowBalloonTip(3000, "NFC Agent Tool", "Listo y escuchando", ToolTipIcon.Info);
            Application.Run();
        }


        // Inicializar al iniciar el programa
        private static void InitLogging()
        {
            // Crear archivo si no existe
            if (!File.Exists(logFilePath))
                File.WriteAllText(logFilePath, $"NFC Agent Tool Log - {DateTime.Now}\n");
        }


        private static void InitTrayIcon()
        {
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Acerca de", null, (s, e) =>
            {
                MessageBox.Show("Elaborado por Royaltics Solutions 2025\nhttps://royaltics.com", "Acerca de", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            portMenuItem = new ToolStripMenuItem($"PORT {PORT}: OFF");
            trayMenu.Items.Add(portMenuItem);

            trayMenu.Items.Add("Ver Logs", null, (s, e) =>
            {
                // Abrir consola y mostrar logs en tiempo real
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
                {
                    Arguments = $"/K type \"{logFilePath}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            });

            trayMenu.Items.Add("Visitar Royaltics", null, (s, e) =>
            {
                Process.Start("https://royaltics.com");
            });

            trayMenu.Items.Add("Salir", null, (s, e) =>
            {
                listener?.Stop();
                trayIcon.Visible = false;
                Application.Exit();
            });

            trayIcon = new NotifyIcon
            {
                Text = "NFCTool Agent",
                Icon = Properties.Resources.logo,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
        }

        private static void ListenLoop()
        {
            while (true)
            {
                try
                {
                    HttpListenerContext ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(o => HandleRequest(ctx));
                }
                catch(Exception ex) {
                    Console.WriteLine(ex.StackTrace);
                    WriteLog(ex.Message);
                    
                }
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";

            try
            {
                string path = ctx.Request.Url.AbsolutePath.ToLower();
                string method = ctx.Request.HttpMethod.ToUpper();
                string body = null;

                if (ctx.Request.HasEntityBody)
                    using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                        body = sr.ReadToEnd();

                if (path.EndsWith("/verifydevice") && method == "GET")
                {
                    bool available = nfc.IsReaderAvailable();
                    WriteJson(ctx, new { status = available, code = 200, message = available ? "Reader activo" : "No disponible" });
                    return;
                }


                if (path.EndsWith("/getuidcard") && method == "GET")
                {
                    String hexUID = nfc.GetUid();
                    WriteJson(ctx, new { status = hexUID != null, code = 200, message = hexUID != null ? "OK" : "Read failed", data = hexUID });
                    return;
                }

                if (path.EndsWith("/readcard") && method == "POST")
                {
                    RequestRead req = JsonConvert.DeserializeObject<RequestRead>(body);
                    byte[] data = nfc.ReadSector(req.Sector, req.AuthKey);
                    string hex = data != null ? BitConverter.ToString(data).Replace("-", "") : null;
                    WriteJson(ctx, new { status = data != null, code = 200, message = data != null ? "OK" : "Read failed", data = hex });
                    return;
                }

                if (path.EndsWith("/writecard") && method == "POST")
                {
                    RequestWrite req = JsonConvert.DeserializeObject<RequestWrite>(body);
                    nfc.WriteSector(req.Sector, req.AuthKey, req.Data);
                    WriteJson(ctx, new { status = true, code = 200, message = "Write OK" });
                    return;
                }

                if (path.EndsWith("/encodingcard") && method == "POST")
                {
                    RequestEncoding req = JsonConvert.DeserializeObject<RequestEncoding>(body);
                    var result = HandleEncoding(req);
                    WriteJson(ctx, result);
                    return;
                }

                ctx.Response.StatusCode = 404;
                WriteJson(ctx, new { status = false, code = 404, message = "Ruta no encontrada" });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                WriteJson(ctx, new { status = false, code = 500, message = ex.Message });
            }
        }


        private static object HandleEncoding(RequestEncoding req)
        {
            if (string.IsNullOrEmpty(req.Uid) || string.IsNullOrEmpty(req.LastDigits) || req.LastDigits.Length != 5)
                return new { status = false, code = 400, message = "uid y lastDigits(5) requeridos" };

            // Compactar UID (hasta 11 chars) + lastDigits (5 chars) en 16 bytes
            string uidPart = req.Uid.Length > 11 ? req.Uid.Substring(0, 11) : req.Uid.PadRight(11, '0');
            string dataCompact = $"{uidPart}{req.LastDigits}"; // total 16 chars

            byte[] payload = Encoding.UTF8.GetBytes(dataCompact);
            if (payload.Length > 16) payload = payload.Take(16).ToArray();
            else if (payload.Length < 16) payload = payload.Concat(new byte[16 - payload.Length]).ToArray();

            string keyA = GenerateRandomHexKey();
            string keyB = GenerateRandomHexKey();

            try
            {
                // Escribir datos en sector y cambiar claves
                nfc.WriteSector(req.Sector, req.AuthKey, Encoding.UTF8.GetString(payload));
                nfc.ChangeKeys(req.Sector, req.AuthKey, keyA, keyB, req.AccessBits);
            }
            catch (Exception ex)
            {
                return new { status = false, code = 500, message = ex.Message };
            }

            return new
            {
                status = true,
                code = 200,
                message = "Encoding realizado",
                data = new
                {
                    encoding = Encoding.UTF8.GetString(payload),
                    keya = keyA,
                    keyb = keyB,
                    sector = req.Sector,
                    uid = req.Uid,
                    lastDigits = req.LastDigits
                }
            };
        }


        private static string GenerateRandomHexKey()
        {
            byte[] key = new byte[6];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(key);
            }
            return BitConverter.ToString(key).Replace("-", "");
        }

 
        private static byte[] GenerateSecretKey()
        {
            byte[] k = new byte[24];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(k);
            return k;
        }

        private static void WriteJson(HttpListenerContext ctx, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            byte[] b = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.OutputStream.Close();
        }


        // Método para escribir log
        private static void WriteLog(string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(logLine); // también a consola
            try
            {
                File.AppendAllText(logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* ignorar errores de escritura */ }
        }


        // DTOs
        public class RequestRead { public int Sector; public string AuthKey; }
        public class RequestWrite { public int Sector; public string AuthKey; public string Data; }
        public class RequestEncoding
        {
            public string Uid;
            public string LastDigits;
            public int Sector;
            public string AuthKey;
            public string NewKeyA;
            public string NewKeyB;
            public int AccessBits;
        }
    }
}
