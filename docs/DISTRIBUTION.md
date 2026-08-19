# AJ Dock Distribution And Windows Trust

## Why Windows Blocked The App

AJ Dock private builds are currently unsigned. Windows Defender SmartScreen and Smart App Control treat new unsigned executables as untrusted because there is no verified publisher identity and no download reputation yet.

An installer, terms screen, or nicer setup wizard does not make Windows trust the app. Those pieces improve the installation experience, but Windows trust comes from distribution reputation and Authenticode code signing.

## Release Options

### Private testing

For early family/friend testing, the current portable zip is acceptable, but testers may need to choose **More info** > **Run anyway** when SmartScreen appears. On some Windows 11 systems with Smart App Control enabled, unsigned files may be blocked more strictly.

### Installer

AJ Dock has an Inno Setup installer script:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.1
```

This requires Inno Setup 6. The installer adds terms, shortcuts, an uninstall entry, and an uninstall confirmation prompt. It does not remove SmartScreen warnings unless the installer and app binaries are signed and gain reputation.

### Signed installer and app

After you obtain a code-signing certificate, sign the published app and installer:

```powershell
$env:AJDOCK_CERT_PASSWORD = "your-pfx-password"
.\scripts\build-installer.ps1 -Version 0.1.1 -Sign -CertificatePath C:\Path\To\AJDock.pfx
```

If the certificate is installed in the Windows certificate store:

```powershell
.\scripts\build-installer.ps1 -Version 0.1.1 -Sign -CertificateThumbprint YOUR_CERT_THUMBPRINT
```

The scripts use SHA-256 and RFC 3161 timestamping so signatures remain valid after the signing certificate expires.

## Best Path To Fewer Warnings

1. Use a real code-signing certificate or Microsoft Trusted Signing.
2. Sign every release consistently with the same publisher identity.
3. Upload the signed installer to GitHub Releases or another stable official download page.
4. Tell private testers to download only from that official page.
5. Consider Microsoft Store distribution later for the cleanest trust experience.

Even signed first releases can still show an "unrecognized app" warning until Microsoft has enough reputation for the file or publisher. Unsigned releases have to build reputation from zero every time.
