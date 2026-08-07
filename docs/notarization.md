# Signing and notarizing releases

Until BlankSlate is notarized by Apple, macOS blocks the first launch on other people's
machines with *"Apple could not verify BlankSlate is free of malware."* Notarizing removes
that warning entirely.

This is a one-time setup. After it, `scripts/package-macos.sh` produces a notarized build
automatically.

## Prerequisites

- An **Apple Developer Program** membership ($99/year). Enrol at
  [developer.apple.com/programs/enroll](https://developer.apple.com/programs/enroll)
  or through the Apple Developer app on iPhone/iPad, which is usually faster.
  Choose **Individual / Sole Proprietor** unless you specifically need a company name
  on the signature, since Organization enrolment additionally requires a D-U-N-S number.
- **Xcode command line tools**, which provide `codesign` and `notarytool`:

  ```sh
  xcode-select --install
  ```

No qualification or review is required to enrol. It is a paid membership.

## Step 1: create a Developer ID Application certificate

This is the certificate for apps distributed **outside** the Mac App Store. It is not the
same as the "Apple Development" or "Apple Distribution" certificates.

Easiest route, using Xcode:

1. Install Xcode, open it, and sign in under **Settings > Accounts** with your Apple ID
2. Select your team, click **Manage Certificates…**
3. Click **+** and choose **Developer ID Application**

Alternatively, create a certificate signing request with Keychain Access and upload it at
[developer.apple.com/account/resources/certificates](https://developer.apple.com/account/resources/certificates).

Confirm it landed in your keychain:

```sh
security find-identity -v -p codesigning
```

You should see a line like:

```
1) A1B2C3... "Developer ID Application: Your Name (ABCDE12345)"
```

The value in quotes is your signing identity. `ABCDE12345` is your **Team ID**.

## Step 2: create an app-specific password

`notarytool` cannot use your normal Apple ID password when two-factor authentication is on.

1. Go to [account.apple.com](https://account.apple.com) and sign in
2. Under **Sign-In and Security**, choose **App-Specific Passwords**
3. Generate one and name it something like `notarytool`
4. Copy the password. It is shown only once.

## Step 3: store the credentials once

```sh
xcrun notarytool store-credentials "notarytool-profile" \
    --apple-id you@example.com \
    --team-id ABCDE12345 \
    --password xxxx-xxxx-xxxx-xxxx
```

This saves the credentials in your keychain, so they never appear in the build script or
in your shell history afterwards.

## Step 4: build a notarized release

Set two environment variables and run the packaging script as usual:

```sh
export BLANKSLATE_SIGN_ID="Developer ID Application: Your Name (ABCDE12345)"
export BLANKSLATE_NOTARY_PROFILE="notarytool-profile"

./scripts/package-macos.sh          # Apple Silicon
./scripts/package-macos.sh x64      # Intel
```

With those set, the script signs with your Developer ID and the hardened runtime, uploads
the build to Apple, waits for the result, staples the ticket to the app, and re-zips it so
the uploaded archive carries the ticket.

Notarization usually takes a few minutes. The script waits for it.

## Step 5: verify before publishing

```sh
# Signature is a Developer ID, not ad-hoc
codesign -dv --verbose=4 dist/BlankSlate.app 2>&1 | grep Authority

# Gatekeeper accepts it
spctl -a -vvv -t install dist/BlankSlate.app

# The notarization ticket is stapled, so it works offline
xcrun stapler validate dist/BlankSlate.app
```

`spctl` should report `accepted` and `source=Notarized Developer ID`.

The real test: unzip the release on a Mac that has never seen the app and double-click it.
It should open with no warning at all.

## Step 6: update the README

Once a notarized build ships, delete the **First launch** section from the README and the
Gatekeeper workaround from the release notes. They no longer apply.

## Troubleshooting

**`notarytool` reports `Invalid`.** Fetch the log, which names the exact problem:

```sh
xcrun notarytool log <submission-id> --keychain-profile "notarytool-profile"
```

The usual causes are a missing hardened runtime (the script sets `--options runtime`) or a
nested binary that was not signed. The script signs with `--deep`, which covers the .NET
runtime files inside the bundle.

**`spctl` rejects it while notarization succeeded.** The ticket is probably not stapled.
Re-run `xcrun stapler staple dist/BlankSlate.app`.

**The certificate is missing after a macOS reinstall.** Developer ID certificates cannot be
re-downloaded with their private key. Export a `.p12` backup from Keychain Access and store
it somewhere safe. Losing it means revoking and reissuing.
