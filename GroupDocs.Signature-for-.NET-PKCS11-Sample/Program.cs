using System.Security.Cryptography.X509Certificates;
using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.Tests;
using Net.Pkcs11Interop.Tests.HighLevelAPI;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== PDF Signing with GroupDocs.Signature & PKCS#11 ===");

        try
        {
            // Load license
            var licensePath = "licPath";
            new License().SetLicense(licensePath);
            Console.WriteLine("[INFO] License applied");
            
            // Getting Started with Pkcs11Interop
            // Specify the path to unmanaged PKCS#11 library provided by the cryptographic device vendor
            string pkcs11LibraryPath = @"c:\SoftHSM2\lib\softhsm2-x64.dll";
            Pkcs11InteropSetup(pkcs11LibraryPath);
        
            string inputPdf = @"c:\input.pdf";
            string outputPdf = @"c:\output.pdf";
            
            // Perform signing
            SignPdfWithX509Certificate2(inputPdf, outputPdf);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Signed file created at: {outputPdf}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] " + ex.Message);
            Console.ResetColor();
        }
    }

    private static void Pkcs11InteropSetup(string pkcs11LibraryPath)
    {
        // Create factories used by Pkcs11Interop library
        Pkcs11InteropFactories factories = new Pkcs11InteropFactories();

        // Load unmanaged PKCS#11 library
        using (IPkcs11Library pkcs11Library =
               factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, pkcs11LibraryPath,
                   AppType.MultiThreaded))
        {
            // Show general information about loaded library
            ILibraryInfo libraryInfo = pkcs11Library.GetInfo();

            Console.WriteLine("Library");
            Console.WriteLine("  Manufacturer:       " + libraryInfo.ManufacturerId);
            Console.WriteLine("  Description:        " + libraryInfo.LibraryDescription);
            Console.WriteLine("  Version:            " + libraryInfo.LibraryVersion);

            // Get list of all available slots
            foreach (ISlot slot in pkcs11Library.GetSlotList(SlotsType.WithOrWithoutTokenPresent))
            {
                // Show basic information about slot
                ISlotInfo slotInfo = slot.GetSlotInfo();

                Console.WriteLine();
                Console.WriteLine("Slot");
                Console.WriteLine("  Manufacturer:       " + slotInfo.ManufacturerId);
                Console.WriteLine("  Description:        " + slotInfo.SlotDescription);
                Console.WriteLine("  Token present:      " + slotInfo.SlotFlags.TokenPresent);

                if (slotInfo.SlotFlags.TokenPresent)
                {
                    // Show basic information about token present in the slot
                    ITokenInfo tokenInfo = slot.GetTokenInfo();

                    Console.WriteLine("Token");
                    Console.WriteLine("  Manufacturer:       " + tokenInfo.ManufacturerId);
                    Console.WriteLine("  Model:              " + tokenInfo.Model);
                    Console.WriteLine("  Serial number:      " + tokenInfo.SerialNumber);
                    Console.WriteLine("  Label:              " + tokenInfo.Label);

                    // Show list of mechanisms (algorithms) supported by the token
                    Console.WriteLine("Supported mechanisms: ");
                    foreach (CKM mechanism in slot.GetMechanismList())
                        Console.WriteLine("  " + mechanism);
                }
            }
        }
    }
    private static void SignPdfWithX509Certificate2(string inputPdf, string outputPdf)
    {
        if (ValidateInputOutputFiles(inputPdf, outputPdf)) return;
        
        // Load certificate from Windows store (e.g., by subject name)
        var store = new X509Store(StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2? cert = store.Certificates
            .Find(X509FindType.FindBySubjectName, "YourCertificateSubject", false)
            .OfType<X509Certificate2>()
            .FirstOrDefault();

        store.Close();

        if (cert == null)
        {
            Console.WriteLine("No certificates were found.");
            return;
        }

        byte[] certBytes = cert.Export(X509ContentType.Cert); // DER format
        using var certStream = new MemoryStream(certBytes);

        Pkcs11DigitalSigner pcs11DigitalSigner = new Pkcs11DigitalSigner();

        using var signature = new Signature(inputPdf);
        DigitalSignOptions signOptions = new DigitalSignOptions(certStream)
        {
            Reason = "Approved",
            Location = "India",
            // certificate password
            Password = "1234567890",
            Contact = "JohnSmith",
            AllPages = true,
            Width = 80,
            Height = 60,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Padding() { Bottom = 10, Right = 10 },
            CustomSignHash = pcs11DigitalSigner
        };

        signature.Sign(outputPdf, signOptions);
    }

    private static bool ValidateInputOutputFiles(string inputPdf, string outputPdf)
    {
        if (!File.Exists(inputPdf))
        {
            Console.WriteLine("The input file does not exist.");
            return true;
        }

        if (string.IsNullOrEmpty(outputPdf))
        {
            Console.WriteLine("The output file name is not specified.");
            return true;
        }

        return false;
    }
}

public class Pkcs11DigitalSigner : ICustomSignHash
{
    public byte[] CustomSignHash(byte[] signableHash, HashAlgorithm hashAlgorithm, SignatureContext signatureContext)
    {
        using IPkcs11Library pkcs11Library =
            Settings.Factories.Pkcs11LibraryFactory.LoadPkcs11Library(Settings.Factories, Settings.Pkcs11LibraryPath,
                Settings.AppType);
        // Find first slot with token present
        ISlot slot = Helpers.GetUsableSlot(pkcs11Library);

        // Open RW session
        using ISession session = slot.OpenSession(SessionType.ReadWrite);
        // Login as normal user
        session.Login(CKU.CKU_USER, Settings.NormalUserPin);

        // Generate key pair
        IObjectHandle publicKey = null;
        IObjectHandle privateKey = null;
        Helpers.GenerateKeyPair(session, out publicKey, out privateKey);

        // Specify signing mechanism
        IMechanism mechanism = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);

        byte[] sourceData = ConvertUtils.Utf8StringToBytes("Hello world");

        // Sign data
        byte[] signature = session.SignRecover(mechanism, privateKey, sourceData);

        // Do something interesting with signature

        // Verify signature
        bool isValid = false;
        // Do something interesting with verification result and recovered data
        byte[] recoveredData = session.VerifyRecover(mechanism, publicKey, signature, out isValid);

        session.DestroyObject(privateKey);
        session.DestroyObject(publicKey);
        session.Logout();

        return signature;
    }
}