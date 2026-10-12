# Verification

## GitHub Actions Windows build and tag releases

- The workflow builds on Windows Server 2022 with the Windows .NET Framework compiler, C# 4 syntax and checksum-pinned Microsoft .NET Framework 4.0 reference assemblies. It compiles a fresh client from staged sources rather than republishing the checked-in executable.
- The first [Windows CI run](https://github.com/IOIgf/Aurora-Proxy/actions/runs/38187305904) passed 43 complete-checkbox-frame checks and 25 integration checks using the bundled Windows mihomo executable. This adds native Windows GDI / checkbox buffer and Windows core-process coverage to the earlier Mono checks.
- Release packaging verifies the bundled core hash, synchronizes staged assembly/footer/README versions with the tag, regenerates per-file checksums, verifies ZIP entries and emits a ZIP checksum. Branch builds retain downloadable artifacts; version-tag builds publish Release assets after successful checks.
- Windows Server 2022 is not Windows 7. Aero/Classic appearance, actual Win7 DPI, IME, tray behavior and system proxy acceptance on Windows 7 remain unverified.

## Executable

- Compiled successfully with Mono C# compiler 6.8, C# 4 syntax, AnyCPU, CLR v4 assembly, using the official Microsoft .NET Framework 4.0 reference assemblies (`Microsoft.NETFramework.ReferenceAssemblies.net40` 1.0.3).
- Windows GUI PE executable includes Win7 compatibility manifest, per-process DPI awareness and application icon.
- References only .NET Framework assemblies; no NuGet packages, WebView or browser runtime.
- Native UI was rendered using Mono WinForms in a virtual X display and inspected in dark and light themes.

## 25 integration checks passed

The tests use the real official Linux `mihomo v1.19.32 / amd64-v1-go120` executable and an in-process local subscription server. They cover:

- Settings and subscription persistence, atomic file replacement.
- App-owned controller, proxy ports, TUN and listener overrides.
- Rejection of raw links, duplicate root fields, multi-document YAML and root merge keys.
- Core validation of generated complete configurations and nodes-only fallback groups.
- Malformed-node rejection without starting the service.
- Subscription HTTP download, Clash User-Agent, UTF-8 names, usage and expiration metadata.
- Real process startup, authenticated REST calls, version response, Unicode group / node selection.
- Global mode switching, invalid reload rejection while preserving the active core and cached profile.
- Valid live YAML payload reload, connection metrics, clean stop and restart.
- Malformed subscription expiry handling.

The official Windows asset SHA-256 was verified against the upstream release API digest.

## 37 UI regression checks passed for 1.0.2

`tests/UiSmokeTests.cs` exercises rendered button backgrounds, primary hover / press feedback, idle animation timer shutdown, Space / Enter activation, disabled action prevention, inherited busy states, reduced-motion behavior, light-theme redraw, page transition docking, resize cancellation and timer cleanup on disposal. The Mono-only asynchronous failure checks dismiss the managed error dialog and verify that failure does not invoke the success callback, content / navigation recover, and the busy indicator stops.

The 1.0.2 additions cover removal of ButtonBase inheritance, all rounded corners on a painted gradient parent, pressed and disabled repaints, movement / resizing, clipped partial repaint, a rounded Surface edge, mouse release inside / outside, lost capture, focused Enter, Escape canceling Space, Alt mnemonics, accessible activation and dialog OK / Cancel results. All 37 checks passed both with and without calling `Application.EnableVisualStyles()` in the Mono virtual display; Mono does not reproduce Windows Aero or Windows Classic rendering.

These are runtime checks in a virtual X display, not Windows 7 native acceptance tests.

## 11 node-interaction checks passed for 1.0.3

`tests/NodeUiTests.cs` uses the real Linux mihomo core and a 120-node group in a rendered MainForm. It exercises the actual “use selected node” button and node double-click handlers, verifies the core's chosen node, and checks that the first visible row, selected / focused item, row object identities and in-use status survive the switch. Additional checks verify that native vertical / horizontal wheel messages and managed wheel input do not change the category, while opening and explicit selection remain available.

These checks ran under Mono in a virtual X display. Native Windows mouse-wheel routing and Win7 scrolling still require real-machine acceptance.

## 1.1.8 complete checkbox frame initialization

- The user clarified that the artifact began when native checkboxes were replaced by `SoftCheckBox` in 1.1.2. That implementation overrides `OnPaint` without calling the platform renderer, but inherited `ControlStyles.Opaque` from ButtonBase. Its `OnPaint` painted only glyphs/text, leaving background initialization solely in `OnPaintBackground`.
- Microsoft's [ControlStyles documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.controlstyles?view=netframework-4.8.1) states that Opaque skips background painting. The official [ButtonBase source](https://source.dot.net/System.Windows.Forms/System/Windows/Forms/Controls/Buttons/ButtonBase.cs.html) enables that flag, and [Control's paint dispatch](https://source.dot.net/System.Windows.Forms/System/Windows/Forms/Control.cs.html) checks it before invoking the background layer. These source links are the current implementation; they do not constitute execution on .NET Framework 4.0 / Windows 7. The pre-fix executable's Opaque flag was also confirmed as true in the available Mono runtime.
- `SoftCheckBox.OnPaint` now initializes its invalidated background before rendering every glyph/text frame. Opaque is explicit and its contract is fulfilled; the standalone background override remains safe for printing paths. Both paths clip to the control/invalidated region, preserve graphics state and never invoke ancestor foreground paint. Native CheckBox state, mouse/key handling and events remain unchanged.
- Added `CheckBoxFrameTests.cs`: 43 checks model the documented Opaque dispatch by omitting the separate background callback, prefill the buffer with magenta and unrelated sibling text, and compare it pixel-for-pixel with a clean reference frame. They cover both themes, unchecked/checked/mixed/disabled/hover/pressed states, shorter labels, partial invalidation, graphics state, neighboring pixels, recreated handles, enlargement and parent-color changes.
- The same compiled regression test fails against saved 1.1.7 at its first dirty-frame comparison and passes with the fix. Earlier tests directly called `OnPaintBackground` or used Mono's `DrawToBitmap`, so they did not cover skipped background dispatch. This explains why their passing results did not establish the missing complete-frame contract.
- Passed 43 checkbox frame, 19 checkbox behavior, 39 repaint, 25 scene, 89 alignment, 26 input, 11 LAN/connection, 37 button/page and 19 node/latency checks (308 total) under Mono/Xvfb. Compiled C# 4 against Microsoft's .NET Framework 4.0 references and refreshed previews.
- The new tests deterministically reproduce stale-buffer retention in the old renderer, not the entire user's native screenshot. No Windows 7 environment was available; Aero/Classic, real HWND/GDI rendering, IME and native DPI acceptance remain outstanding.

## 1.1.7 palette-era scene repaint regression

- Compared archived 1.1.2 and 1.1.3 source: the palette update also split navigation text into two draw calls. Restored a single bounded draw while retaining the palette. The 1.1.6 fix had left button ancestor paint calls and transparent card backgrounds in place.
- Buttons and cards now use opaque native backgrounds and a background-only rendering contract. Child backgrounds read colors, background images and explicit `IBackgroundLayer` geometry; they do not invoke ancestor `OnPaint` / `OnPaintBackground` or copy foreground controls. The existing gradient-parent smoke fixture implements this contract, retaining all its corner, movement and partial-invalidation assertions.
- All custom TextRenderer calls now use bounded clipping with graphics clipping / translation preservation. Each helper restores the caller's graphics state. Input editing remains native.
- Added `SceneUiTests.cs`: 25 checks cover ancestor foreground isolation for buttons/cards, nested tinted cards, both themes, partial invalidation, preserved graphics transforms/clips, neighboring pixels and oversized translated text. The same compiled regression test fails against the saved 1.1.6 executable at the first ancestor-foreground assertion and passes against the new implementation.
- Passed 25 scene, 39 repaint, 89 alignment, 26 input, 19 checkbox, 37 button/page, 11 LAN/connection and 19 node/latency checks (265 total) under Mono/Xvfb. Compiled C# 4 against Microsoft's .NET Framework 4.0 references; refreshed dark/light preview images.
- These are controlled regressions, not reproduction of the complete Windows screenshot. Windows 7 Aero/Classic, native HWND/GDI text clipping, IME and actual DPI scaling remain unverified on a real machine.

## 1.1.6 repaint isolation and native spinner clipping

- The supplied Windows screenshot shows unrelated navigation/label text repeated inside checkbox backgrounds and a square native remnant inside numeric fields. This is distinct from the geometry/font changes in 1.1.5.
- Input and checkbox backgrounds now paint their local opaque page/card color without invoking ancestor background/content drawing. NumericUpDown is clipped to the editable area while excluding the native spinner slot, including layouts that expand the EDIT after hiding the spinner.
- Added `RepaintUiTests.cs`: 39 checks cover ancestor repaint isolation, dark/light and tinted backgrounds, clipped partial invalidation, parent color changes, editable numeric text, increments/decrements, enlarged geometry, handle recreation and repeated state changes.
- The repaint-isolation regression fails on the saved 1.1.5 executable at the parent-drawing assertion and passes on 1.1.6. The controlled test injects parent paint content; it does not reproduce the full native Windows transparency pipeline from the screenshot.
- Final verification: 39 repaint, 89 alignment, 26 input, 19 checkbox, 37 button/page, 11 LAN/connection and 19 node/latency checks passed (240 total) under Mono/Xvfb. Rebuilt with C# 4 against Microsoft's .NET Framework 4.0 references and inspected refreshed previews.
- Actual Windows 7 Aero/Classic rendering, native HWND clipping and IME remain real-machine acceptance items. No Windows environment was available, so the screenshot-specific outcome is not claimed as native acceptance-tested.

## 1.1.5 field and checkbox alignment

- Reproduced the default-font mismatch: an unparented wrapped TextBox retained the 8.25 pt system default while its page used 9 pt. Default fonts now inherit from the page; explicit fonts remain intact.
- Added `AlignmentUiTests.cs`: 89 checks cover font inheritance and changes, editor positioning after scaling, all setting/LAN row centers and containment, checkbox preferred space, theme stability, repeated navigation and subscription dialog spacing.
- Controlled 100/125/150/200% enlargements scale both geometry and fonts. Mono omits absolute TableLayoutPanel style scaling, so the test explicitly applies captured row/column sizes once. Xvfb DPI settings still report 96 DPI to Mono: these are enlarged-layout tests, **not native Windows DPI emulation**.
- Final input/checkbox/alignment suites passed after adding native GDI font metrics and EM_GETRECT formatting offsets. The 37 button/page, 11 LAN/connection and 19 node/latency checks also passed with the layout changes. Existing integration and protocol code was unchanged.
- Inspected final settings, LAN and subscription controls, including 150% enlarged layouts. Compiled C# 4 against Microsoft's .NET Framework 4.0 reference assemblies.
- Windows-specific WM_GETFONT, GetTextMetricsW and EM_GETRECT paths, true system DPI scaling, Aero/Classic and IME remain real-machine acceptance items; the build environment does not contain Windows 7.

## 1.1.4 latency viewport regression

- Expanded `NodeUiTests.cs` to 19 checks. Existing wheel and node-selection checks use the real mihomo process; eight new checks exercise the single-node / group latency buttons, async refresh, timeout display, unchanged category, manual refresh, filtered lists and surviving viewport / selection / focus after membership changes.
- Delay tests use a local deterministic HTTP controller with a running real core, avoiding external test-site availability. Returned delay values are synthetic test inputs, not measurements of proxy performance.
- The expanded test fails on the 1.1.3 executable at the single-node latency viewport assertion and passes on 1.1.4 with a 120-node list. Test cleanup now disposes the core even on assertion failures.
- Built against Microsoft's .NET Framework 4.0 reference assemblies with C# 4, and inspected the updated node page. Native Win7 scrolling remains a real-device acceptance item.

## 1.1.3 palette verification

- Re-ran the 37 button / page, 19 checkbox, 26 input and 11 LAN / connection UI checks after palette updates.
- Inspected dark and light overview, settings, LAN, nodes and subscription dialog screenshots. Native input backgrounds and child-container backgrounds use the same theme tokens as their painted surfaces.
- Calculated relative-luminance contrast: primary button white text, secondary text on cards and each metric color on its tinted card exceed 4.5:1 in both themes. These are palette checks, not a claim of complete accessibility conformance.
- Rebuilt with C# 4 against Microsoft's .NET Framework 4.0 reference assemblies; native Windows 7 rendering remains unverified.

## 1.1.2 checkbox and text alignment verification

- `CheckBoxUiTests.cs`: 19 checks cover custom glyph / parent background, single-event click and Space toggles, mnemonics, accessibility checked / mixed states, real control message dispatch for glyph and label clicks, disabled colors / focus, AutoCheck, theme changes, reduced motion, timer cleanup and 150% scaling.
- `InputUiTests.cs` now checks 26 behaviors, including font-line centering for plain text and NumericUpDown's nested editor. Re-ran the 11 LAN / connection UI checks with the custom checkbox controls.
- Inspected dark / light LAN and settings screens plus the subscription dialog. Compiles with C# 4 against Microsoft's .NET Framework 4.0 reference assemblies.
- Native Windows 7 text rasterization, IME, DPI and accessibility still need real-device acceptance; the verification environment is Mono / Xvfb.

## 1.1.1 input verification

- `InputUiTests.cs` checks 24 input behaviors: preserved placement, inset native editors, rounded parent background, Unicode selection replacement, password masking / reveal, focus feedback and timer cleanup, disabled behavior, native dropdown opening and selection, hidden system spinners, custom increments / decrements with range limits, theme changes, repeated application, scaling, subscription dialog spacing, LAN fields and compact settings rows.
- Re-ran the button / page, node interaction and LAN / connection UI suites against the final executable. No user configuration or subscriptions were used.
- Built against Microsoft's .NET Framework 4.0 reference assemblies with C# 4. Screenshots inspected in Mono / Xvfb for dark and light themes, LAN, settings, nodes, overview and the subscription dialog.
- These checks do not replace real Windows 7 validation of native IME, high-DPI behavior, screen-reader navigation, popup rendering and system theme differences.

## 1.1.0 networking verification

- 29 checks in `tests/NetworkTests.cs` passed using official Linux mihomo: legacy-setting defaults, LAN and escaped-password persistence, credential validation, owned LAN/auth fields, private-IPv4 classification, quoted startup command, password-free instructions, core configuration validation, shared/local-only binding, local authentication bypass, HTTP 407/403 rejection and success, SOCKS5 credential acceptance/rejection, live connection metadata, single/all connection closure and explicit no-auth mode.
- The environment has only a loopback interface. Protocol authentication was tested by temporarily removing the loopback exemption through the core API, then restoring it. Shared binding and allow-lan values were verified through the real controller; cross-device traffic and source-CIDR enforcement have **not** been tested on a real LAN.
- 11 checks in `tests/NetworkUiTests.cs` passed: LAN defaults, masked password, control enabled states, saving fields, wheel-safe adapter chooser, source/traffic rendering, incremental connection-list updates preserving rows/scroll/selection, stale-row removal, startup opt-in and all seven navigation entries fitting the minimum window.
- Existing 25 integration checks, 37 button/page checks and 11 node-interaction checks passed against the 1.1.0 executable. The project compiles against Microsoft's .NET Framework 4.0 reference assemblies.
- Native Windows adapter discovery, firewall allowances, DPAPI protection and HKCU login startup remain unverified. Startup means launching the tray client, not automatically enabling the kernel or system proxy.

## Requires Windows 7 acceptance testing

Windows 7 itself is not available in the build environment. The following must be checked on the user's machine:

- Launch on Win7 SP1, with .NET 4.x and the included x64 core; 32-bit systems need a 386 core.
- WinINet system proxy / PAC / automatic-detection snapshot and restoration.
- Restoring prior settings on normal exit, kernel failure and recovery after client crash.
- Job Object child cleanup, tray interactions, native window scaling and high-DPI layout.
- Button appearance on Aero and Classic themes, actual mouse / keyboard input and dialog validation; Mono does not implement `ContainerControl.Validate(bool)`, so Windows validation cancellation requires native acceptance.
- HTTPS subscription downloads with the machine's Win7 TLS and certificate updates.
- Real provider subscriptions and real remote proxy connectivity.

No live user subscription, proxy credentials or personal network settings were used in the integration tests.
