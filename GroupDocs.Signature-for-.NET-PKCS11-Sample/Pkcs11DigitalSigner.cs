using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace GroupDocs.Signature_for_.NET_PKCS11_Sample;

/// <summary>
///     Implements ICustomSignHash for GroupDocs.Signature and uses Pkcs11Interop v5 to sign the externally computed hash
///     with a private key on the token. It returns the signature bytes to GroupDocs which will embed them into the PDF.
/// </summary>
public class Pkcs11DigitalSigner : ICustomSignHash
{
    // Configure these static fields before use (Program sets them)
    public static string Pkcs11LibraryPath = null!;
    public static string UserPin = null!;
    public static AppType AppType = AppType.MultiThreaded;

    public byte[] CustomSignHash(byte[] signableHash, HashAlgorithm hashAlgorithm, SignatureContext signatureContext)
    {
        if (signableHash == null) throw new ArgumentNullException(nameof(signableHash));

        // Build PKCS#1 DigestInfo for externally hashed data (CKM_RSA_PKCS expects DigestInfo)
        var digestInfo = BuildDigestInfo(hashAlgorithm, signableHash)
                         ?? throw new NotSupportedException($"Unsupported hash algorithm: {hashAlgorithm}");

        // Create factories and load PKCS#11 library (Pkcs11Interop v5 pattern)
        var factories = new Pkcs11InteropFactories();
        using (var pkcs11Library =
               factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, Pkcs11LibraryPath, AppType))
        {
            // Find a slot with token present
            var slot = GetFirstUsableSlot(pkcs11Library);
            if (slot == null) throw new Exception("No usable PKCS#11 slot found.");

            using (var session = slot.OpenSession(SessionType.ReadWrite))
            {
                // Login as a normal user (required to access private objects on most tokens)
                session.Login(CKU.CKU_USER, UserPin);

                try
                {
                    // Try to find a certificate on token and get its CKA_ID so we can match private key
                    var certId = TryGetFirstCertificateId(session);

                    // Build private key search template (prefer matching by CKA_ID if available)
                    var keyTemplate = new List<IObjectAttribute>
                    {
                        session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
                        session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SIGN, true)
                        // optionally, restrict to RSA keys:
                        // session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, CKK.CKK_RSA)
                    };
                    if (certId != null && certId.Length > 0)
                        keyTemplate.Add(session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, certId));

                    var foundKeys = TryFindAllObjects(session, keyTemplate);
                    if (foundKeys.Count == 0)
                    {
                        // If no key found with CKA_SIGN or matching ID, try looser search (any private key)
                        var fallbackTemplate = new List<IObjectAttribute>
                        {
                            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY)
                        };
                        foundKeys = TryFindAllObjects(session, fallbackTemplate);
                    }

                    if (foundKeys == null || foundKeys.Count == 0)
                        throw new Exception("No private key found on the token.");

                    var privateKey = foundKeys[0];

                    // Use mechanism CKM_RSA_PKCS for externally hashed data (DigestInfo must be provided)
                    var mechanism = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);

                    // Sign digestInfo using the token private key
                    var signature = session.Sign(mechanism, privateKey, digestInfo);

                    return signature;
                }
                finally
                {
                    try
                    {
                        session.Logout();
                    }
                    catch
                    {
                        /* ignore */
                    }
                }
            }
        }
    }

    // Try to read the first certificate object CKA_ID from token (optional)
    private static byte[]? TryGetFirstCertificateId(ISession session)
    {
        try
        {
            var certTemplate = new List<IObjectAttribute>
            {
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE),
                session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CERTIFICATE_TYPE, CKC.CKC_X_509)
            };

            var certs = TryFindAllObjects(session, certTemplate);
            if (certs.Count > 0)
            {
                IList<IObjectAttribute> attrs = session.GetAttributeValue(certs[0], new List<CKA> { CKA.CKA_ID });
                if (attrs != null && attrs.Count > 0)
                {
                    var id = attrs[0].GetValueAsByteArray();
                    if (id != null && id.Length > 0) return id;
                }
            }
        }
        catch
        {
            // ignore and return null
        }

        return null;
    }

    // Fallback helper: try FindAllObjects() first, else fallback to FindObjectsInit/FindObjects
    private static IList<IObjectHandle> TryFindAllObjects(ISession session, List<IObjectAttribute> template)
    {
        try
        {
            return session.FindAllObjects(template);
        }
        catch
        {
            // fallback pattern
            session.FindObjectsInit(template);
            var found = session.FindObjects(20); // up to 20 handles
            session.FindObjectsFinal();
            return found;
        }
    }

    // Return the first slot with a present token
    private static ISlot? GetFirstUsableSlot(IPkcs11Library pkcs11Library)
    {
        var slots = pkcs11Library.GetSlotList(SlotsType.WithTokenPresent);
        foreach (var s in slots)
        {
            var si = s.GetSlotInfo();
            if (si != null && si.SlotFlags.TokenPresent)
                return s;
        }

        return null;
    }

    // Build DigestInfo prefix and hash according to hash algorithm (PKCS#1 v1.5)
    private static byte[]? BuildDigestInfo(HashAlgorithm hashAlgorithm, byte[] hash)
    {
        // DigestInfo prefixes (DER) for common algorithms
        byte[] prefix;
        var algName = hashAlgorithm.ToString().ToUpperInvariant() ?? string.Empty;

        if (algName.Contains("SHA1") || algName == "SHA-1")
        {
            prefix = new byte[]
                { 0x30, 0x21, 0x30, 0x09, 0x06, 0x05, 0x2B, 0x0E, 0x03, 0x02, 0x1A, 0x05, 0x00, 0x04, 0x14 };
        }
        else if (algName.Contains("SHA256") || algName == "SHA-256")
        {
            prefix = new byte[]
            {
                0x30, 0x31, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00,
                0x04, 0x20
            };
        }
        else if (algName.Contains("SHA384") || algName == "SHA-384")
        {
            prefix = new byte[]
            {
                0x30, 0x41, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x02, 0x05, 0x00,
                0x04, 0x30
            };
        }
        else if (algName.Contains("SHA512") || algName == "SHA-512")
        {
            prefix = new byte[]
            {
                0x30, 0x51, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x03, 0x05, 0x00,
                0x04, 0x40
            };
        }
        else
        {
            return null;
        }
        
        // unsupported algorithm
        using var ms = new MemoryStream(prefix.Length + hash.Length);
        ms.Write(prefix, 0, prefix.Length);
        ms.Write(hash, 0, hash.Length);
        
        return ms.ToArray();
    }
}