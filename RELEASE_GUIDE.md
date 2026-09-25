# 🚀 Release Checklist & Guide

**⚠️ CRITICAL WARNING FOR RELEASING UPDATES ⚠️**

When you upload a new release to GitHub, **DO NOT INCLUDE THE VERSION NUMBER IN THE `.zip` FILE NAME!**

- ❌ **BAD:** `SmartNotes-v1.1.5-win-x64.zip` (This will BREAK the download links in the README!)
- ✅ **GOOD:** `SmartNotes-win-x64.zip`

Because the `README.md` download links point to `latest/download/SmartNotes-win-x64.zip`, the file uploaded to your new release must have exactly that name, otherwise the links will stop working.

---

### How to Build and Publish a New Release:

1. **Update the Version Number:**
   Open `SmartNotes.csproj` and `Core/Services/UpdateService.cs` and update the version number to the new version.

2. **Compile the Self-Contained App:**
   Run the following command in your terminal to compile the app so users don't need to install the .NET runtime:
   ```bash
   dotnet publish -c Release
   ```

3. **Zip the Output:**
   Go to the `bin/Release/net10.0-windows/win-x64/publish/` folder.
   Select all files in that folder and compress them into a ZIP file.

4. **Name the ZIP File Correctly:**
   Rename your zip file to exactly:
   **`SmartNotes-win-x64.zip`**

5. **Upload to GitHub Releases:**
   Create a new release on GitHub, attach your `SmartNotes-win-x64.zip` file, and publish!
