# QuickBuzz — Technical Analysis

**Audited artifact:** `C:\Sharique\Projects\Personal\QuizApp\QuickBuzz`
**Audit date:** 2026‑09‑02
**Method:** Full read of every source file (9 `.cs`/`.cshtml`/`.js`/`.json` files, ~14 KB of code). Vendor libraries under `wwwroot/lib/` excluded from review.

---

## 1. What this project is

QuickBuzz is a **standalone ASP.NET Core (.NET 10) MVC web app** that talks to a physical buzzer/timer rig over a serial (RS‑485‑style) connection and displays team reaction times on three screens: **Buzzer** (single button, team E), **QuickBuzz** (first of buttons A–D), and **Test** (raw dump of all buttons A–E per device). It is the hardware‑facing companion intended to sit alongside `QuizApp-9AMM` (the show‑control app analyzed separately in [QuizApp-9AMM-Technical-Analysis.md](QuizApp-9AMM-Technical-Analysis.md)).

**Solution contents:** one project, `QuickBuzz.Web` (`QuickBuzz.slnx` references nothing else). Inside it: 2 controllers, 1 background/singleton service pair, 2 models, 5 views, 1 layout, ~4.5 KB of hand‑written JS, and the standard `dotnet new mvc` scaffolding (Bootstrap 5.3, jQuery 3.7, jQuery Validation — all vendored under `wwwroot/lib/`).

### 1.1 Architecture as built

```
Browser (operator, 3 pages)
   │  fetch() → JSON
   ▼
DeviceApiController  (api/device/*)
   │
   ▼
SerialService (singleton, in-memory, holds one open SerialPort)
   │  38400 baud, 24-byte fixed frames, custom binary protocol
   ▼
Arduino / RS-485 buzzer hardware  (3 physical devices, IDs 1–3)
```

- `Program.cs` registers `SerialService` as a DI **singleton** — correct choice, since it owns one persistent `SerialPort` handle that must survive across requests.
- `HomeController` only ever returns empty views (`Index`, `Buzzer`, `QuickBuzz`, `Test`, `Privacy`, `Error`) — all page logic lives in inline `<script>` blocks in the `.cshtml` files, which call the JSON API directly with `fetch()`.
- `DeviceApiController` (`/api/device/*`) is the only real logic surface: `ports`, `connect`, `start`, `reset`, `test`, `quickbuzz`, `buzzer`.
- `SerialService` encodes the wire protocol: a 24‑byte frame (`0x02` start, device ID, command byte, 4× big‑endian 32‑bit millisecond timers for buttons A–D... plus E, `0xFD`/`0xFE` end byte), parsed by the static `DeviceParser.Parse`.

### 1.2 Architecture as documented — mismatch

`readme.md` describes a **4‑project layered architecture** (`QuickBuzz.Web` / `.Application` / `.Domain` / `.Infrastructure` / `.Tests`) with DDD‑style separation, repository pattern, and a dedicated test project. **None of this exists.** `QuickBuzz.slnx` contains exactly one project, and there are no `Domain`, `Application`, `Infrastructure`, or `Tests` folders anywhere on disk. `SerialService`/`DeviceParser` sit under `QuickBuzz.Web/Services/`, functioning as infrastructure code inside the presentation project.

**This is not necessarily wrong** — for an app this size (one hardware integration, three display pages), a single project is the right amount of engineering, and the README's 4‑layer design would be over‑engineering for the current scope. But the README will actively mislead the next person who opens the repo expecting that structure. **Recommendation: rewrite the README to describe what actually exists** (a single ASP.NET Core MVC project, `Controllers/Api` for the hardware API, `Services/` for the serial integration), and reintroduce the layered README only if/when the project actually grows into it.

---

## 2. The critical finding: QuickBuzz is not connected to QuizApp‑9AMM

There is **no code‑level integration** between the two projects. `QuizApp-9AMM`'s `BuzzerController`/`FirstRoundController`/`SecondRoundController`/`FinalRoundController` know nothing of `QuickBuzz`'s `/api/device/*` endpoints, and `QuickBuzz` has no knowledge of `QuizApp-9AMM`'s scoring model, teams, matches, or rounds. They are two independent web apps (`http://localhost:2028` and `http://localhost:5250`) that would run in separate browser tabs during a show.

**Practical consequence:** when a team buzzes in, QuickBuzz shows the reaction time and rank on its own screen — but that result is **not written anywhere**. The operator must read the QuickBuzz display and then manually key the outcome into `QuizApp-9AMM`'s buzzer‑answer screen (`FirstRound/MatchOneBuzzer`, etc.), which itself grades from a client‑side JavaScript boolean (see the QuizApp‑9AMM report, finding SEC‑03). QuickBuzz's precise millisecond timing — the entire point of the hardware — never reaches the database.

This is the single most important gap in the pairing. It is presumably why the two projects still exist independently rather than merged: QuickBuzz appears to be a newer, in‑progress rewrite that has not yet been wired into the scoring flow.

**Recommendation (the actual "missing feature"):** either (a) have `QuickBuzz` POST the ranked result to a new `QuizApp-9AMM` endpoint once server‑side grading exists there (see that report's §3.2/§14 R6), or (b) invert it — merge `QuickBuzz`'s serial‑device layer into `QuizApp-9AMM` directly (both target broadly the same era of the show) — or (c), the cleanest long‑term path already implied by both codebases: migrate `QuizApp-9AMM` to .NET 8+/ASP.NET Core and merge the two into one solution, with `SerialService` as the buzzer input source and the existing scoring tables as the output. This is called out as Phase 5 in the QuizApp‑9AMM report.

---

## 3. Confirmed defects

| # | Defect | Evidence | Impact |
|---|---|---|---|
| 1 | **`site.js` is entirely dead code** | `wwwroot/js/site.js` binds handlers to `#connect-btn`, `#qb-start`, `#qb-reset`, `#qb-get`, `#bz-start`, `#bz-reset`, `#bz-get` and POSTs to `/Home/ConnectArduino`, `/Home/QuickBuzzStart`, `/Home/QuickBuzzReset`, `/Home/QuickBuzzGetData`, `/Home/BuzzerStart`, `/Home/BuzzerReset`, `/Home/BuzzerGetData`. **None of these element IDs exist in any view, and none of these actions exist on `HomeController`** (which has only `Index`/`Buzzer`/`QuickBuzz`/`Test`/`Privacy`/`Error`). The file is loaded on every page via `_Layout.cshtml:25` but every handler is a silent no‑op. | Confusing to a future maintainer; dead weight loaded on every page load |
| 2 | **No overall timeout on frame synchronization** | `SerialService.ReadDevice`: `do { b = _port!.ReadByte(); } while (b != 0x02);` — each individual `ReadByte()` respects the 1500 ms `ReadTimeout`, but if the port keeps delivering *some* byte within every 1.5 s window without ever producing `0x02` (line noise, wrong baud, a half‑connected cable), **this loop has no overall deadline and can spin indefinitely** | A single bad read can hang a `PollAll()` call (and the HTTP request behind it) with no way to recover except restarting the app |
| 3 | **Fully synchronous, blocking serial I/O on the request thread** | No `async`/`await` anywhere in `SerialService` or `DeviceApiController` despite `Test()`/`QuickBuzz()`/`Buzzer()` all synchronously calling `_serial.PollAll()`, which polls 3 devices sequentially with up to 3 retries each (1500 ms read timeout per attempt) plus a mandatory 150 ms bus delay per device | Worst case (all three devices unresponsive): ~3 × 3 × 1.5 s + delays ≈ **13+ seconds blocking a thread‑pool thread** per `Get Data` click. At one operator this is merely a bad UX (the button appears to hang); it does rule out ever adding a second concurrent client |
| 4 | **Unhandled exception path in the API layer** | `ReadDeviceWithRetry` → `SendCommand` (`_port.Write(...)`) is **not** wrapped in `try/catch`. If the cable is unplugged mid‑poll, `Write` throws (e.g. `IOException`/`InvalidOperationException`), which propagates up through `PollAll()` into `DeviceApiController.Test/QuickBuzz/Buzzer` — none of which have a `try/catch` — resulting in an unhandled 500 with no graceful message on the operator's screen | A mid‑show cable fault crashes the request instead of showing "device disconnected" |
| 5 | **`DeviceParser.Parse` returns `null` but is typed as non‑nullable** | `public static DeviceTimes Parse(byte[] data) { if (data.Length < 24) return null; ... }` — the project has `<Nullable>enable</Nullable>`, so this is a live nullable‑reference warning (CS8603) the build is currently swallowing/ignoring | Low practical risk today (the only caller, `SerialService.ReadDevice`, always passes an exactly‑24‑byte buffer), but the signature lies about its own contract; should be `DeviceTimes?` |
| 6 | **Client input trusted directly into `SerialPort` construction** | `DeviceApiController.Connect([FromBody] PortRequest req)` passes `req.Port` straight to `_serial.Connect(port)` → `new SerialPort(port, ...)` with no check against `SerialPort.GetPortNames()` first | Low severity (opening an arbitrary COM port name is not a meaningful attack surface locally), but it's unvalidated input reaching a native resource handle with no allow‑list |
| 7 | **A commented‑out duplicate `Buzzer()` action sits directly above the live one** | `DeviceApiController.cs:104‑114` | Minor hygiene issue — leftover from an edit, harmless but should be deleted |
| 8 | **`Privacy.cshtml` / `Privacy()` action are unreferenced template boilerplate** | `HomeController.Privacy()` exists and `Views/Home/Privacy.cshtml` exists, but `_Layout.cshtml` has no navigation and no view links to `/Home/Privacy` anywhere | Leftover from the `dotnet new mvc` scaffold; harmless, but dead surface area |

None of the above is remotely as severe as the QuizApp‑9AMM findings (no auth issue here is meaningful — the endpoints only reach local hardware, not a scoring database — and there is no injection/XSS surface). This project's risks are **reliability/availability**, not integrity or security.

---

## 4. Duplication

The three gameplay views are near‑identical:

| File | Shared with | What differs |
|---|---|---|
| `Views/Home/Buzzer.cshtml` | `QuickBuzz.cshtml` | Only the fetch URL (`/api/device/buzzer` vs `/api/device/quickbuzz`) and one line of display formatting (`format(d.time)` vs `` `${d.key} - ${format(d.time)}` ``) — otherwise byte‑for‑byte the same 3‑team grid + footer + `getData()`/`clearUI()`/`format()` functions |
| `Views/Home/Test.cshtml` | both of the above | Same footer control block (`reset`/`start`/`get` buttons, `format()` helper, `clearUI()`) reimplemented a third time, rendering a table instead of the 3‑team grid |

All three duplicate the same "Reset / Start / Get Data" footer markup and the same `format()` time‑formatting function inline. This is the same copy‑paste pattern documented at larger scale in the QuizApp‑9AMM report.

**Recommendation:** extract a `_ControlFooter.cshtml` partial for the reset/start/get buttons, and move `format()`/`clearUI()`/the `start`/`reset`/`get` click handlers into a shared `wwwroot/js/gameplay.js` (parameterized by the endpoint path: `buzzer` | `quickbuzz` | `test`). This is a half‑day cleanup given the project's current size — worth doing now, before a fourth screen is added and the pattern calcifies the way it did in QuizApp‑9AMM.

---

## 5. What's done well

- **Correct DI lifetime choice.** `SerialService` as a singleton is the right call for a component owning one persistent hardware handle; the .NET DI container will call its `Dispose()` on app shutdown automatically (it implements `IDisposable`), so cleanup is handled correctly without extra code.
- **Thread‑safety around the port.** All `SerialService` methods that touch `_port` take a `lock (_lock)` — correct given the singleton is shared across concurrent requests.
- **Retry logic with bus delay.** `ReadDeviceWithRetry` + the mandatory 150 ms `Thread.Sleep` between devices in `PollAll` reflects real RS‑485 bus settling behavior — this wasn't guessed, it was tuned against real hardware.
- **No injection/XSS surface.** The API is JSON‑in/JSON‑out with no raw HTML rendering of user input, no SQL, no file writes.
- **Modern, supported stack.** .NET 10, nullable reference types enabled, `ImplicitUsings` — unlike QuizApp‑9AMM's .NET Framework 4.6.1, this project has no framework‑obsolescence debt.

---

## 6. Testing & reliability

Zero test coverage — no test project, no `[Fact]`/`[Test]`, matching the pattern in `QuizApp-9AMM`. Given the project's size, the highest‑value target for a first test pass is **`DeviceParser.Parse`**: it's a pure function (byte array in, `DeviceTimes` out) that decodes a hand‑rolled binary protocol via manual bit‑shifting — exactly the kind of logic that's easy to get subtly wrong (endianness, off‑by‑one offsets) and trivial to unit test with a handful of known‑good byte arrays. No infrastructure or mocking is required.

---

## 7. Prioritized recommendations

| # | Action | Why | Effort |
|---|---|---|---|
| 1 | Add a hard deadline to the start‑byte search loop in `ReadDevice` (e.g. abort after N attempts or an overall `Stopwatch` cutoff) | Currently unbounded — a live‑show‑ending hang risk (§3 #2) | 1 h |
| 2 | Wrap `PollAll`/`SendCommand` call sites in the controller (or inside `SerialService`) with `try/catch`, returning a JSON error the UI can show | A disconnected cable currently crashes the request with no operator‑visible message (§3 #4) | 2 h |
| 3 | Delete `wwwroot/js/site.js` (or replace it with real handlers if it was meant to back the current pages) | It is unreferenced dead code loaded on every page (§3 #1) | 15 min |
| 4 | Make `SerialService`/`DeviceApiController` async (`Task<IActionResult>`, `SerialPort.BaseStream.ReadAsync`) | Removes the thread‑pool‑blocking risk if the app ever serves more than one concurrent operator/tab (§3 #3) | 1 day |
| 5 | Validate `req.Port` against `SerialPort.GetPortNames()` before connecting | Defense in depth on the one endpoint that touches an OS resource by name (§3 #6) | 15 min |
| 6 | Fix `DeviceParser.Parse`'s return type to `DeviceTimes?` and have the one caller handle it explicitly | Nullable annotations currently lie about the contract (§3 #5) | 15 min |
| 7 | Delete the commented‑out duplicate `Buzzer()` action and the unused `Privacy` page | Hygiene (§3 #7, #8) | 15 min |
| 8 | Extract the shared footer/format/handler code out of `Buzzer.cshtml`/`QuickBuzz.cshtml`/`Test.cshtml` into a partial + shared JS module | Stops the 3‑way copy‑paste from becoming a 5‑way one (§4) | 4 h |
| 9 | Rewrite `readme.md` to describe the actual single‑project structure | The current README describes a 4‑project architecture that was never built and will mislead the next maintainer (§1.2) | 1 h |
| 10 | Add unit tests for `DeviceParser.Parse` | Only pure, easily‑testable logic in the project; currently 0% covered (§6) | 2 h |
| 11 | Design and implement the actual score hand‑off to `QuizApp-9AMM` | This is the load‑bearing missing feature — without it, the hardware's precision timing never reaches the scoreboard (§2) | Depends on which integration direction is chosen; see §2 |

**Total for items 1–10 (everything except the cross‑app integration): roughly 2 days.** Item 11 is a design decision, not just an engineering task — it depends on whether the two apps are meant to stay separate (needs a small API contract between them) or eventually merge (see the QuizApp‑9AMM report's Phase 5 modernization plan, which this project would naturally fold into).

---

*Report generated 2026‑09‑02. No code was modified as part of this analysis.*
