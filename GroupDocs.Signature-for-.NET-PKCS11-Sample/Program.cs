using System.Security.Cryptography.X509Certificates;
using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace GroupDocs.Signature_for_.NET_PKCS11_Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== PDF Signing with GroupDocs.Signature & PKCS#11 (Pkcs11Interop v5) ===");

            try
            {
                // -------------- FILL THESE VALUES --------------
                string licensePath = @"C:\path\to\license.lic"; // or null if you don't use license
                string pkcs11LibraryPath = @"C:\path\to\watchdata-pkcs11.dll"; // your PKCS#11 DLL
                string userPin = "123456"; // user PIN for the token
                // -----------------------------------------------

                // apply a license if you have one
                if (File.Exists(licensePath))
                {
                    new License().SetLicense(licensePath);
                    Console.WriteLine("[INFO] GroupDocs license applied.");
                }

                // configure signer static settings used inside Pkcs11DigitalSigner
                Pkcs11DigitalSigner.Pkcs11LibraryPath = pkcs11LibraryPath;
                Pkcs11DigitalSigner.UserPin = userPin;

                string inputPdf = @"C:\temp\input.pdf";
                string outputPdf = @"C:\temp\output_signed.pdf";

                SignPdfWithX509Certificate2(inputPdf, outputPdf);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Signed file created at: {outputPdf}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] " + ex);
                Console.ResetColor();
            }
        }

        private static void SignPdfWithX509Certificate2(string inputPdf, string outputPdf)
        {
            // basic validation
            if (!File.Exists(inputPdf))
            {
                Console.WriteLine("Input PDF does not exist: " + inputPdf);
                return;
            }

            // load certificate from Windows store by subject name (adjust as needed)
            var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            // 1. Try Windows store first
            X509Certificate2? cert = store.Certificates
                .Find(X509FindType.FindBySubjectName, "YourCertificateSubject", false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();

            store.Close();

            // 2. If not found in a store, try reading from a token
            if (cert == null)
            {
                Console.WriteLine("[WARN] Certificate not found in Windows store. Trying to fetch directly from token...");

                cert = LoadCertificateFromToken();
            }
            
            // final check
            if (cert == null)
                throw new Exception("Certificate could not be retrieved from either Windows store or token.");

            // export public certificate (DER) to stream — GroupDocs uses this to embed public certificate in the signed PDF
            byte[] certBytes = cert.Export(X509ContentType.Cert);
            using var certStream = new MemoryStream(certBytes);

            // prepare GroupDocs signature and options
            using var signature = new Signature.Signature(inputPdf);

            var pkcs11Signer = new Pkcs11DigitalSigner(); // implements ICustomSignHash

            var signOptions = new DigitalSignOptions(certStream)
            {
                Reason = "Approved",
                Location = "Office",
                Contact = "Admin",
                AllPages = false,
                Width = 160,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Padding() { Bottom = 10, Right = 10 },
                CustomSignHash = pkcs11Signer
            };

            // perform signing - GroupDocs will compute hash and call CustomSignHash
            signature.Sign(outputPdf, signOptions);
        }

        private static X509Certificate2 LoadCertificateFromToken()
        {
            X509Certificate2? cert;
            var factories = new Pkcs11InteropFactories();
            using (var pkcs11Library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(
                       factories, Pkcs11DigitalSigner.Pkcs11LibraryPath, Pkcs11DigitalSigner.AppType))
            {
                var slot = pkcs11Library.GetSlotList(SlotsType.WithTokenPresent).FirstOrDefault();
                if (slot == null) throw new Exception("No PKCS#11 slot with token found.");

                using (var session = slot.OpenSession(SessionType.ReadOnly))
                {
                    session.Login(CKU.CKU_USER, Pkcs11DigitalSigner.UserPin);

                    try
                    {
                        // Search for certificate object on the token
                        var certTemplate = new List<IObjectAttribute>
                        {
                            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE),
                            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CERTIFICATE_TYPE, CKC.CKC_X_509)
                        };

                        var certObjects = session.FindAllObjects(certTemplate);
                        if (certObjects.Count == 0)
                            throw new Exception("No certificate found on the token.");

                        // Read raw certificate value
                        var attrs = session.GetAttributeValue(certObjects[0], new List<CKA> { CKA.CKA_VALUE });
                        var rawCert = attrs[0].GetValueAsByteArray();
                        cert = new X509Certificate2(rawCert);
                    }
                    finally
                    {
                        session.Logout();
                    }
                }
            }

            return cert;
        }
    }
}
