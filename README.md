# GroupDocs.Signature-for-.NET-PKCS11-Sample

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://products.groupdocs.com/comparison/python-net/) 
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/comparison/python-net/) 
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/category/comparison/) 
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/comparison) 
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temporary-license)


## 📖 About This Repository

This repository demonstrates how to sign PDF documents with GroupDocs.Signature for .NET using a hardware token via Pkcs11Interop. It shows how to implement ICustomSignHash so that GroupDocs computes the hash while the token performs the signing. Certificates can be loaded from the Windows store or directly from the PKCS#11 device. The private key never leaves the token, ensuring secure digital signatures. This project serves as a reference for integrating PKCS#11-based signing into .NET applications. Use it to learn how to combine GroupDocs’ signing features with hardware-backed security.

### What is GroupDocs.Signature?

**GroupDocs.Signature for .NET** is a powerful document signing API that enables developers to apply, verify, and manage electronic signatures in a wide variety of file formats (PDF, Word, Excel, images, presentations, etc.). It supports not only text, image, stamp, barcode and QR-code signatures, but also full digital signatures using **X.509/PKCS certificates**. 
The library is UI-less and works purely via code, giving fine control over signature appearance, position, and document permissions. It also includes features for searching for existing signatures, validating certificate chains, and protecting documents with password or other security settings.

### What is Pkcs11Interop?

PKCS#11 is a cryptographic standard maintained by the OASIS PKCS 11 Technical Committee (originally published by RSA Laboratories). It defines an ANSI C API to access smart cards and other types of cryptographic hardware.

Pkcs11Interop is a managed library written in C# that brings the full power of the PKCS#11 API to the .NET environment. It loads the unmanaged PKCS#11 library provided by the cryptographic device vendor and makes its functions accessible to .NET applications.

Pkcs11Interop is a free, open-source library. You can find more details [here](https://github.com/Pkcs11Interop/Pkcs11Interop)

## How It Works

This repository demonstrates how to sign PDF documents in .NET using **GroupDocs.Signature** together with a hardware token (dongle) via **Pkcs11Interop**. The signing process combines the convenience of GroupDocs with the security of PKCS#11 hardware tokens.

[![diagram](https://raw.githubusercontent.com/groupdocs-signature/GroupDocs.Signature-for-.NET-PKCS11-Sample/master/imges/customhash_pkcs11_diagram.png)]


1. **Provide a PDF Document** – You start by specifying the PDF file that you want to sign.  
2. **GroupDocs Computes the Hash** – The library calculates a cryptographic hash of the document, which is the data that will actually be signed.  
3. **Access the Token via PKCS#11** – Using Pkcs11Interop, the application loads the PKCS#11 library provided by your hardware token vendor and opens a secure session with the token.  
4. **Find the Private Key on the Token** – The token searches for the private key that corresponds to your certificate. If the certificate isn’t registered in Windows, it is retrieved directly from the token.  
5. **Sign the Hash Securely** – The document hash is sent to the token. The token performs the signing operation internally, ensuring the private key never leaves the hardware.  
6. **Embed the Signature into the PDF** – GroupDocs.Signature receives the signed hash and embeds it into the PDF file, producing a fully signed document ready for use.

This approach ensures maximum security by keeping your private key on the hardware token while providing a fully automated signing workflow in .NET applications.

## Getting Started / Installation
### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/your-org/your-repo.git
   cd your-repo
   ```
2. Open the project in **Visual Studio** or **JetBrains Rider**.  
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Update `Program.cs` with:
   - Path to your PKCS#11 library (`watchdata-pkcs11.dll`, `softhsm2.dll`, etc.)  
   - Your token PIN  
   - Certificate subject name (if using Windows certificate store)  

### Run the sample

```bash
dotnet run
```

A signed PDF will be generated at the output path you configured in `Program.cs`.


## Prerequisites
- **.NET 6.0** or later  
- A valid **PKCS#11 library DLL** provided by your token vendor  
- A **hardware token (dongle)** with a valid certificate issued by a Certifying Authority (CA)  
- (Optional) **GroupDocs temporary license** – [Get one here](https://purchase.groupdocs.com/temporary-license/)  


## Repository Structure

```
GroupDocs.Signature-for-.NET-PKCS11-Sample/
│
├── GroupDocs.Signature-for-.NET-PKCS11-Sample.csproj # Project file (.NET 6)
├── Program.cs # Entry point, configures signing process
├── Settings.cs # Holds PKCS#11 settings (DLL path, PIN, etc.)
├── Helpers.cs # Utility methods for working with Pkcs11Interop
├── Pkcs11DigitalSigner.cs # Implements ICustomSignHash using PKCS#11
└── README.md # Documentation
```

- **Program.cs** – Main program. Loads settings, certificate, and runs PDF signing with GroupDocs.Signature.  
- **Settings.cs** – Stores PKCS#11 configuration like library path and user PIN in one place.  
- **Helpers.cs** – Helper utilities for working with slots, certificates, and PKCS#11 objects.  
- **Pkcs11DigitalSigner.cs** – Connects GroupDocs.Signature with the dongle. Implements custom signing via PKCS#11.  

This structure makes it easier to **separate PKCS#11 token handling** from the **GroupDocs.Signature logic**.  

## Use Cases

This project is designed for developers and organizations that need to sign PDF documents with **hardware tokens (dongles)** using **GroupDocs.Signature**.  

A common scenario is when a user has a **digital signature dongle issued by a Certifying Authority (CA)**. For example:  

> *“I have a digital signature dongle issued to me by a Certifying Agency (CA) in India. The digitally signed documents using the dongle are legally binding in India. I want to use this dongle for signing documents digitally in the GroupDocs viewer using the digital signature functionality. This is also required by my clients in India. For signing the PDF document using the dongle in Acrobat Reader I have to provide the password for the dongle.”*  

This sample solves exactly that problem:  

- **GroupDocs.Signature** provides the document signing framework (hashing, embedding the signature, managing PDF structure).  
- **Pkcs11Interop** enables communication with the dongle (loading the PKCS#11 library, unlocking with your PIN, finding the private key, and performing the signing).  
- The **private key never leaves the dongle**, ensuring compliance with legal and security requirements.  
- The signed PDF is **legally valid** because the signature comes directly from the dongle’s certified key, just like in Acrobat Reader.  

With this approach, you can showcase to clients how GroupDocs integrates with hardware-backed digital signatures, making it suitable for use cases in regions like India where **CA-issued dongle signatures are legally binding**.  

## ⚠️ Important Note

This solution is currently provided as an **early implementation** for using PKCS#11 digital signature dongles with GroupDocs.Signature.  
While it enables document signing with hardware tokens, **we strongly recommend performing additional testing in your own environment** to ensure it meets your compliance and security requirements.  

We would greatly appreciate your feedback, test results, and suggestions for improvements.  
Your input will help us refine this functionality and explore new ideas to make it more robust and user-friendly.

## Keywords

`digital signature`, `electronic signature`, `document signing`, `PDF signing`, `API`, `.NET`, `Java`, `C#`, `document automation`, `e-signature`, `digital certificates`, `barcode signatures`, `QR code signatures`, `form fields`, `document security`, `compliance`, `ESIGN`, `eIDAS`, `signature verification`, `cloud API`, `on-premise`, `multi-format support`, `enterprise solution`, `PKCS#11`, `USB dongle`, `hardware token`, `certificate store`, `X.509 certificate`, `private key protection`, `token-based authentication`, `cryptographic device`, `digital signature dongle India`, `CA India`, `legally binding signature`
