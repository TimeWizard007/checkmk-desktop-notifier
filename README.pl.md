# Checkmk Desktop Notifier

**[English](README.md)** | **[Polski](README.pl.md)**

Lekki monitor i powiadamiacz pulpitu dla Checkmk (Windows + macOS).

To **niezależny projekt open source**. **Nie jest powiązany z Checkmk GmbH**, nie jest przez nią sponsorowany ani nie jest jej produktem. Nazwa „Checkmk” opisuje wyłącznie system monitoringu, z którym współpracuje ta aplikacja.

Aktualna wersja: **1.3.0** — pierwsze wspólne wydanie Windows + macOS. **FEATURE COMPLETE. CURRENT DEVELOPMENT CYCLE CLOSED. FEATURE FREEZE.** Zob. [docs/RELEASE_NOTES_1.3.0.md](docs/RELEASE_NOTES_1.3.0.md).

Historyczne tagi `v1.2.0` i `v1.3.0-beta.1` pozostają bez zmian.

## Przegląd

Checkmk Desktop Notifier to towarzysz per-user dla **Windows 10/11** i **macOS 12+**. Odpytuje Checkmk przez REST API i pokazuje bieżące problemy HARD hostów i usług.

Na Windows używa kompaktowego paska Always-on-Top i zasobnika. Na macOS jest natywną aplikacją **paska menu**. **Nie zastępuje** interfejsu WWW Checkmk.

Stan **Seen** jest lokalny dla tego użytkownika systemu. Opcjonalne **Przejmij** zapisuje w Checkmk trwałe ACK, żeby inni administratorzy widzieli, że problem jest obsługiwany. **Zwolnij** na przejęciu CDN usuwa to ACK w Checkmk. Ręcznego/ogólnego ACK notifier nie usuwa.

## Zrzuty ekranu

Nazwy hostów i wewnętrzne adresy URL są pominięte albo zastąpione przykładami.

![Lista problemów z NEW / CRIT / WARN / UNKNOWN, Przejmij, Seen i Otwórz w Checkmk](docs/images/problem-list-v1.2.png)

![Filtr PRZEJĘTE, globalny licznik PRZEJĘTE i Przejęte przez](docs/images/taken-filter-v1.2.png)

![Ciemne potwierdzenie Przejmij](docs/images/take-dialog-v1.2.png)

![Ciemne potwierdzenie Zwolnij](docs/images/release-dialog-v1.2.png)

![Ustawienia — Ogólne / koordynacja zespołu](docs/images/settings-team-v1.2.png)

![Ustawienia — Połączenie](docs/images/settings-connection.png)

![Ustawienia — Powiadomienia](docs/images/settings-notifications-v1.2.png)

![Menu zasobnika systemowego](docs/images/tray-menu-v1.2.png)

## Funkcje

- Kompaktowy pasek Always-on-Top z licznikami NEW / CRIT / WARN / UNKNOWN / TAKEN
- Rozwijana lista problemów (hosty i usługi, wyjście pluginu, interfejs EN/PL)
- Klikalne liczniki i chipy filtrów WSZYSTKIE / NOWE / CRIT / WARN / NIEZNANE / PRZEJĘTE (tylko prezentacja; incydenty nie są scalane)
- Wyszukiwanie na żywo nad listą problemów (host, usługa, nazwa Przejęte przez; łączy się z aktywnym filtrem)
- Lokalne NEW / Seen (per użytkownik Windows, zapisywane na dysku; odwracalne Unseen)
- Otwórz odpowiadający host lub usługę w GUI Checkmk (domyślna przeglądarka; nie zmienia stanu incydentu)
- Opcjonalne Przejmij / współdzielone trwałe ACK w Checkmk (domyślnie wyłączone)
- Znacznik ACK Checkmk (zwykłe ACK albo Przejęte przez nazwę wyświetlaną)
- Znaczniki zaplanowanego przestoju
- Odpytywanie w tle kolekcji REST usług i hostów
- Powiadomienia pulpitu i dźwięk alertu (dymki zasobnika Windows; natywne powiadomienia macOS)
- Dołączony WAV, opcjonalny własny WAV, głośność aplikacji, wyciszenie
- Grupowanie powiadomień HOST DOWN / UNREACHABLE (tylko powiadomienia)
- Ustawienia w GUI; sekret automatyzacji w **Menedżerze poświadczeń Windows** albo **Keychain** na macOS
- Autostart: Windows HKCU Run albo LaunchAgent użytkownika na macOS, który otwiera `.app`
- Pojedyncza instancja: drugie uruchomienie aktywuje istniejący proces

**Windows**

- Kompaktowy pasek Always-on-Top i zasobnik (pokaż / ukryj / Ustawienia / O programie / Wycisz / Zakończ)
- Instalator per-user (bez uprawnień Administratora)
- Nadal obsługiwany jest przenośny publikowany build win-x64

**macOS**

- Natywny element paska menu z licznikami NEW / CRIT / WARN / UNK / TAKEN na żywo
- Panel problemów; zamknięcie okna nie kończy aplikacji (użyj Quit)
- Jednostką dystrybucji jest `Checkmk Desktop Notifier.app` (nie surowy plik wykonywalny)

## Wymagania

**Windows**

- Windows 10 lub Windows 11 (64-bit)
- Do instalacji i zwykłego użycia **nie** są potrzebne uprawnienia Administratora
- Sieciowy dostęp do serwera Checkmk (np. VPN, jeśli tak łączycie się z witryną)

**macOS**

- macOS 12 lub nowszy
- Intel x64 (zweryfikowane na prawdziwym urządzeniu) albo Apple Silicon arm64 (build dostępny; weryfikacja na fizycznym urządzeniu może trwać po wydaniu)
- Sieciowy dostęp do serwera Checkmk (np. VPN, jeśli tak łączycie się z witryną)

**Checkmk**

- Zweryfikowano wobec **Checkmk CRE / RAW 2.4.0p34**, REST API 1.0
- Inne edycje/wersje z tymi samymi kolekcjami POST usług i GET hostów mogą działać; nie są zgłaszane jako przetestowane
- Użytkownik automatyzacji, który **odczytuje** hosty i usługi, które Was interesują (zob. [Użytkownik automatyzacji Checkmk](#użytkownik-automatyzacji-checkmk))
- Opcjonalne Przejmij wymaga też uprawnienia Checkmk **`action.acknowledge`** (nie Administrator)

## Instalacja

Zalecane przy zwykłym użytku: instalator per-user `CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe` z GitHub Release.

- Uruchamia się jako **zwykły użytkownik Windows**. Bez UAC / uprawnień Administratora.
- Instaluje do `%LocalAppData%\Programs\CheckmkDesktopNotifier`
- Zawsze tworzy skrót w **menu Start**
- Opcjonalny **skrót na pulpicie** (domyślnie wyłączony)
- Opcjonalne **Uruchamiaj z systemem Windows** (ta sama wartość HKCU Run co Ustawienia → Ogólne)
- **Nie** wymaga `checkmk.local.json`, `CHECKMK_CONFIG` ani zmiennych środowiskowych

Instalator i aplikacja współdzielą jeden mechanizm autostartu:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`  
nazwa wartości: `CheckmkDesktopNotifier`  
polecenie: ścieżka do `CheckmkDesktopNotifier.exe` w cudzysłowie

Dla tej opcji nie ma skrótu w folderze Autostart, zadania Harmonogramu zadań ani wpisu HKLM.

Przed uruchomieniem porównaj sumę instalatora z `SHA256SUMS-v1.3.0.txt` z GitHub Release. Historyczna suma instalatora v1.2.0 pozostaje w [SHA256SUMS.txt](SHA256SUMS.txt).

## macOS

macOS to towarzysz **paska menu**, który współdzieli Core / Infrastructure z Windows. **Nie** jest klonem pływającego paska Windows.

**Pobranie** (sumy SHA-256 w `SHA256SUMS-v1.3.0.txt` na GitHub Release):

- Intel: `CheckmkDesktopNotifier-macOS-x64-v1.3.0.dmg`
- Apple Silicon: `CheckmkDesktopNotifier-macOS-arm64-v1.3.0.dmg`

1. Otwórz DMG.
2. Przeciągnij `Checkmk Desktop Notifier.app` do Applications.
3. Wysuń DMG.
4. Uruchom z Applications.

Jeśli Gatekeeper zablokuje pierwsze uruchomienie: **kliknięcie prawym → Open → Open**. **Nie** wyłączaj Gatekeepera, SIP ani innych zabezpieczeń macOS. Aplikacja jest niepodpisana i nienotaryzowana.

**Zachowanie**

- Element paska menu z licznikami NEW / CRIT / WARN / UNK / TAKEN na żywo
- Panel problemów: filtry, wyszukiwanie, Seen/Unseen, Przejmij / Przejęte przez / Zwolnij, Otwórz w Checkmk
- Odpytywanie w tle przy ukrytych oknach; wyjście tylko przez **Quit**
- Sekret automatyzacji wyłącznie w **Keychain**
- Uruchamianie przy logowaniu przez LaunchAgent użytkownika, który otwiera `.app`
- Natywne powiadomienia i dźwięk (ta sama polityka co Windows; dostarczanie jest natywne dla macOS)
- Pojedyncza instancja: drugie uruchomienie aktywuje istniejący proces

Historyczne ZIP-y testerskie (`v1.3.0-beta.1`) pozostają w [docs/RELEASE_NOTES_1.3.0-beta.1.md](docs/RELEASE_NOTES_1.3.0-beta.1.md). Preferuj DMG v1.3.0.

### Niepodpisany instalator / SmartScreen

Binaria V1 są **niepodpisane**. Windows SmartScreen może pokazać ostrzeżenie o „nieznanym wydawcy”. Oznacza to brak podpisu Authenticode; **samo w sobie nie oznacza**, że plik jest złośliwy. Pobieraj wyłącznie z oficjalnego źródła tego repozytorium i weryfikuj SHA-256 z `SHA256SUMS-v1.3.0.txt` (GitHub Release). **Nie** wyłączaj SmartScreen globalnie.

## Pierwsze uruchomienie / konfiguracja

1. Uruchom **Checkmk Desktop Notifier** z menu Start.
2. Otwórz **Ustawienia** (koło zębate na pasku albo zasobnik → Ustawienia połączenia).
3. Na karcie **Połączenie** uzupełnij:

   | Pole | Znaczenie |
   |------|-----------|
   | Adres URL serwera Checkmk | Tylko origin, np. `https://checkmk.example.com` — bez ścieżki witryny i bez poświadczeń |
   | Witryna (Site) | Nazwa witryny Checkmk, np. `mysite` |
   | Nazwa użytkownika | Nazwa użytkownika automatyzacji |
   | Sekret automatyzacji | Sekret automatyzacji (w Menedżerze poświadczeń, nie w `settings.json`) |
   | Interwał odpytywania | Sekundy między cyklami (domyślnie 60, minimum 10) |

4. Kliknij **Testuj połączenie**. Aplikacja sprawdza, czy kolekcje REST usług **i** hostów są osiągalne.
5. Kliknij **Zapisz**. Monitoring startuje od razu pierwszym odpytaniem.

Zainstalowana aplikacja **nie** potrzebuje `config/checkmk.local.json`, `CHECKMK_CONFIG` ani zmiennych `CHECKMK_*`. To pozostaje mechanizmem **dla deweloperów / CI**.

Przy **pierwszym udanym** odpytaniu z pustym lokalnym stanem incydentów bieżące problemy są wczytywane **cicho** (bez burzy powiadomień). Kolejne cykle powiadamiają tylko nowo otwarte lokalne incydenty.

## Użytkownik automatyzacji Checkmk

Utwórz **użytkownika automatyzacji** z **sekretem automatyzacji** (nie logowaniem hasłem interaktywnym).

Sprawdzony model:

- Rola: **Normal monitoring user** wystarcza dla monitorowanego zakresu (zweryfikowano dla odczytu)
- Członkostwo w grupach kontaktów musi obejmować hosty i usługi, które mają być widoczne
- Uprawnienia Administratora Checkmk **nie** są wymagane
- Opcjonalne Przejmij wymaga **`action.acknowledge`**. Zostaw Przejmij wyłączone, jeśli konto jest tylko do odczytu; monitoring działa dalej
- Węższa rola „tylko te dwie kolekcje” **nie** była testowana

Nie umieszczaj w tym repozytorium prawdziwej nazwy użytkownika, sekretu, URL-a ani wewnętrznej nazwy grupy.

## Seen vs Przejmij vs ACK

**NEW / Seen** jest **lokalne, per użytkownik Windows**:

- Przycisk oka oznacza **ten** incydent jako Seen
- Na wierszu Seen to samo oko oznacza **Unseen** i od razu wraca do NEW
- Oznaczenie Unseen **nie** odtwarza dymka ani dźwięku
- **Oznacz wszystkie nowe jako zobaczone** dotyczy wszystkich obecnie NEW (nie ma masowego Unseen)
- Seen jest zapisywane na dysku i **przetrwa restart**
- Seen **nie** jest wysyłane do Checkmk
- Seen **nie** jest współdzielone między administratorami ani innymi użytkownikami Windows

Kompaktowa ikona **Otwórz w Checkmk** otwiera odpowiadający **widok GUI** hosta albo usługi w domyślnej przeglądarce (nie zasób REST API). Wiersze usług otwierają tę usługę; wiersze hostów otwierają ten host. Nie zmienia Seen, Przejmij, ACK ani przestoju.

**Przejmij** to **współdzielona** akcja zespołowa (Ustawienia → Ogólne, domyślnie wyłączone):

- Tworzy trwałe ACK Checkmk tylko dla tego hosta albo tej usługi (`sticky=true`, `persistent=false`, `notify=false`)
- Nie ukrywa problemu, nie zmienia wagi, nie oznacza Seen, nie ACK-uje usług potomnych i nie tworzy zgłoszenia
- Checkmk przestaje wysyłać kolejne powiadomienia dla bieżącego problemu do powrotu do OK/UP
- Po potwierdzeniu wiersz pokazuje **Przejmowanie...**, aż odczyt z Checkmk potwierdzi **Przejęte przez &lt;nazwa&gt;**. Nie ma optymistycznego stanu Przejęte i nie ma natywnego MessageBox Windows
- Wiele instancji notyfikatora widzi ten sam stan Przejęte po odpytaniu (Checkmk jest źródłem prawdy)

Kliknięcie **Przejęte przez &lt;nazwa&gt;** **zwalnia** przejęcie CDN (każdy administrator korzystający z notyfikatora, nie tylko osoba, która przejęła):

- Zwolnij usuwa to ACK w Checkmk (`POST /domain-types/acknowledge/actions/delete/invoke`) po świeżym odczycie stanu, gdy to praktyczne
- Dozwolone **tylko** dla przejęć CDN utworzonych przez notyfikator. Ręczne/ogólne ACK zostaje nieklikalnym znacznikiem **ACK** i nigdy nie jest usuwane
- Wiersz pokazuje **Zwalnianie...**, aż Checkmk zgłosi brak ACK, potem wraca zwykłe **Przejmij**. Nie ma optymistycznego stanu Zwolnione
- Zwolnij **nie** rozwiązuje problemu w Checkmk. Waga zostaje CRIT/WARN/UNKNOWN, aż Checkmk zgłosi odzyskanie. Sam Checkmk może znów zacząć wysyłać powiadomienia
- Zwolnij nie zmienia lokalnego Seen/Unseen i nie emituje dymka ani dźwięku notyfikatora
- ACK kończy się też przy powrocie do OK/UP

**Przejęte przez** pokazuje osobę tylko gdy komentarz ACK pochodzi z Checkmk Desktop Notifier (`cdn.v1 take name="..."`). Ręczne ACK w Checkmk pokazuje **ACK**, nie zgadywaną osobę. Autorem komentarza w Checkmk jest wspólne konto automatyzacji i nigdy nie jest źródłem tożsamości. Komentarz Take jest **jednoliniowy** (`Taken by {nazwa} via Checkmk Desktop Notifier cdn.v1 take name="..."`), bo Checkmk RAW 2.4 obcina wieloliniowe komentarze ACK.

Nazwa wyświetlana jest w `preferences.json` (nie w Menedżerze poświadczeń).

## Powiadomienia

Dymek Windows (przez ikonę w zasobniku) oraz — o ile nie wyciszono — jeden dźwięk alertu pojawiają się, gdy lokalny incydent **się otwiera** (`AlertDelta.Opened`) po grupowaniu awarii hosta.

- Kolejne odpytania tego samego nieprzerwanego incydentu nie powtarzają dymka/dźwięku
- Restart nie odtwarza już otwartych incydentów
- Nieudane odpytanie nigdy nie wygląda jak „wszystko wróciło do normy” i nie emituje dźwięku odzyskania
- Wyciszenie wyłącza **dźwięk**; dymki nadal się pojawiają
- Jeśli nowy incydent jest **już potwierdzony** w Checkmk w chwili otwarcia, pozostaje lokalnie NEW, ale **nie** ma dymka ani dźwięku
- ACK pojawiające się później na już otwartym incydencie nie tworzy nowego powiadomienia
- Przejmij i Zwolnij **same z siebie** nie emitują dymka ani dźwięku
- Zaplanowany przestój **nie** wycisza dymków (bez zmian)

## ACK i przestój Checkmk

- Opcjonalne **Przejmij** zapisuje trwałe ACK Checkmk tylko dla tego obiektu (zob. [Seen vs Przejmij vs ACK](#seen-vs-przejmij-vs-ack))
- ACK z GUI Checkmk lub innego narzędzia pokazuje **ACK**, nie Przejęte przez
- **Zaplanowany przestój** to metadane **tylko do odczytu**. Ten powiadamiacz nie ustawia ani nie usuwa przestoju
- ACK **to nie** Seen. Oznaczenie Seen **nie** wykonuje ACK w Checkmk

## Grupowanie HOST DOWN / UNREACHABLE

Grupowanie dotyczy **wyłącznie powiadomień**. Incydenty **nie** są scalane w silniku ani na liście problemów.

Jeśli host w stanie HARD **DOWN** (Critical) lub **UNREACHABLE** (Unknown) ma w tym samym zrzucie powiązane usługi:

- Emitowany jest **jeden** zgrupowany dymek hosta i **jeden** dźwięk (`HOST DOWN` / `HOST UNREACHABLE` plus liczba usług)
- Incydenty usług potomnych pozostają w pełni widoczne i mają własne NEW/Seen
- Dymki/dźwięki usług potomnych są wstrzymane, dopóki grupowanie tego hosta jest aktywne
- Kolejne odpytania, gdy host nadal jest niedostępny, nie powtarzają zgrupowanego dymka
- To zapobiega burzy powiadomień usług przy awarii hosta

ACK na hoście grupującym wycisza zgrupowany dymek/dźwięk. Incydenty potomne zostają widoczne i zachowują lokalne NEW/Seen; nie są automatycznie ACK-owane. Przestój **nie** wycisza zgrupowanego dymka.

## Zachowanie zasobnika

Aplikacja trzyma ikonę w zasobniku systemowym.

- Zamknięcie kompaktowego paska **ukrywa** go (nie kończy programu)
- **Otwórz** w zasobniku lub kliknięcie LPM przywraca / przełącza istniejący pasek
- Zasobnik i koło zębate współdzielą Ustawienia, O programie, Wycisz, Ukryj i **Zakończ**
- **Zakończ** to jedyna zwykła droga, by zatrzymać odpytywanie

Drugie uruchomienie zainstalowanego lub przenośnego exe **aktywuje** istniejącą instancję. Nie startuje drugiego odpytywania.

## Uruchamianie z systemem Windows

Ustawienia → **Ogólne** → **Uruchamiaj z systemem Windows** albo pole wyboru w instalatorze zapisują tę samą wartość HKCU Run opisaną w [Instalacja](#instalacja). Pole wyboru pokazuje **rzeczywisty** wpis systemu, nie plik preferencji. Bez podnoszenia uprawnień.

## Dźwięk powiadomienia / własny WAV / głośność / wyciszenie

- Dołączony oryginalny WAV (`notifier.wav`, krótki motyw syntetyczny)
- Ustawienia → **Powiadomienia**: **Domyślny dźwięk powiadomienia** vs **Własny WAV**, głośność **0–100%** (domyślnie 30%), **Wycisz dźwięk**, **Testuj dźwięk powiadomienia**, **Przywróć domyślny dźwięk**
- V1 akceptuje **tylko WAV** (nieskompresowany PCM). MP3/MP4 są celowo niewspierane
- Własny plik jest **kopiowany** do `%LocalAppData%\CheckmkDesktopNotifier\assets\custom-notification.wav`. Usunięcie oryginalnego źródła nie psuje odtwarzania
- Wyciszenie wyłącza wyłącznie dźwięk; dymki nadal się pojawiają
- Test odtwarza wybrany dźwięk przy ustawionej głośności i **omija Wycisz**, żeby dało się posłuchać przy wyciszeniu
- Głośność to skalowanie próbek PCM w procesie. Aplikacja nie zmienia głośności systemowej Windows

## Bezpieczeństwo / Menedżer poświadczeń

| Dane | Lokalizacja |
|------|-------------|
| Ustawienia GUI bez sekretu (URL, witryna, użytkownik, interwał) | `%LocalAppData%\CheckmkDesktopNotifier\settings.json` |
| Sekret automatyzacji | Menedżer poświadczeń Windows, poświadczenie ogólne **`CheckmkDesktopNotifier`** (ten użytkownik Windows) |
| Incydenty / Seen | `%LocalAppData%\CheckmkDesktopNotifier\state\<connection-hash>\alert-state.json` |
| Wyciszenie / głośność / Domyślny vs Własny / Przejmij / nazwa wyświetlana | `%LocalAppData%\CheckmkDesktopNotifier\preferences.json` |
| Zaimportowany WAV | `%LocalAppData%\CheckmkDesktopNotifier\assets\custom-notification.wav` |

- Sekret **nie** jest przechowywany w `settings.json` ani `alert-state.json`
- Nagłówki Authorization **nie** są zapisywane
- Uprawnienia Administratora nie są wymagane
- Menedżer poświadczeń to magazyn sekretów systemu dla tego użytkownika Windows. To **nie** jest szyfrowanie warstwy aplikacji i nie zastępuje tokenu sprzętowego ani firmowego sejfu sekretów

Reset konfiguracji usuwa ustawienia GUI i zapisany sekret. **Nie** kasuje plików incydentów/Seen ani preferencji powiadomień.

## Aktualizacja

Uruchom nowszy `CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe` na istniejącej instalacji per-user.

- Zastępuje pliki programu w `%LocalAppData%\Programs\CheckmkDesktopNotifier`
- Zachowuje dane użytkownika w `%LocalAppData%\CheckmkDesktopNotifier`
- **Nie** usuwa sekretu z Menedżera poświadczeń
- Jeśli aplikacja działa, Setup prosi o **Zakończ** z zasobnika (bez cichego zabijania procesu)

## Odinstalowanie

Użyj **Aplikacje** w Windows albo deinstalatora z folderu instalacji.

- Usuwa binaria, skróty menu Start / pulpitu oraz wartość HKCU Run
- **Domyślnie zachowuje** dane użytkownika (ustawienia, Seen, preferencje, własny WAV)
- Opcjonalne pytanie (domyślnie **Nie**) może usunąć folder aplikacji w LocalAppData i spróbować `cmdkey /delete:CheckmkDesktopNotifier`

## Tryb przenośny

Nadal obsługiwany jest samodzielny publikowany build **win-x64** (`publish/win-x64/CheckmkDesktopNotifier.exe`).

| | Zainstalowany | Przenośny |
|---|---------------|-----------|
| Typowe użycie | Zwykłe wdrożenie | Testy, rozwój, ręczna kopia |
| Lokalizacja | `%LocalAppData%\Programs\CheckmkDesktopNotifier` | Folder, do którego publikujesz |
| Ustawienia / Seen / sekret | Ten sam LocalAppData + Menedżer poświadczeń | Ten sam LocalAppData + Menedżer poświadczeń |

Build przenośny i zainstalowany współdzielą mutex pojedynczej instancji oraz ten sam katalog danych tego użytkownika Windows.

## Budowa ze źródeł

Wymagania: **.NET 8 SDK**.

Aplikacja WPF celuje w `net8.0-windows`. Agent Linux może ją **skompiłować** (`EnableWindowsTargeting`); do **uruchomienia** UI potrzebny jest Windows.

```bash
dotnet build CheckmkDesktopNotifier.sln
dotnet test CheckmkDesktopNotifier.sln
```

Publikacja przenośna:

```bash
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o publish/win-x64
```

`publish/` jest w `.gitignore`. Nie commituj wyniku publikacji.

## Budowa instalatora

Na **Windows**, z zainstalowanym [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
powershell -File scripts/build-windows-package.ps1
```

Wynik (gitignored):

```text
artifacts\CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe
```

Skrypt czyta wersję z `Directory.Build.props` (obecnie **1.3.0**) i przekazuje `/DMyAppVersion` do `iscc`. Równoważnie:

```text
iscc /DMyAppVersion=1.3.0 installer\CheckmkDesktopNotifier.iss
```

SHA-256 zbudowanego instalatora (nie wymyślaj sumy, zanim plik powstanie):

```powershell
powershell -File scripts/hash-windows-installer.ps1
```

albo:

```powershell
Get-FileHash .\artifacts\CheckmkDesktopNotifier-Setup-x64-v1.3.0.exe -Algorithm SHA256
```

Sam Inno Setup **nie** jest commitowany. Źródłem jest tylko `installer/CheckmkDesktopNotifier.iss`.

## Obecne ograniczenia

To **świadome granice V1**, nie przypadkowe braki:

- Windows 10/11 64-bit oraz macOS 12+ (Intel x64 i Apple Silicon arm64)
- Specyficzne dla Checkmk (kolekcje REST opisane w `docs/CHECKMK_API.md`)
- Lokalne Seen **nie** jest współdzielone między administratorami
- Opcjonalne Przejmij zapisuje trwałe ACK w Checkmk; **Zwolnij** usuwa tylko przejęcie CDN (nigdy ręcznego/ogólnego ACK)
- Brak integracji zgłoszeń / Zoho
- Brak własnej współdzielonej bazy / backendu
- Własne dźwięki alertu: **tylko WAV**
- Powiadomienia to **dymki** zasobnika, nie spakowane toasty Windows App SDK
- Binaria są **niepodpisane**; SmartScreen może ostrzegać
- Pozycja paska nie jest zapisywana między restartami
- Brak przełącznika języka w aplikacji (UI idzie za kulturą interfejsu Windows: angielski domyślnie, polski gdy UI culture to polski)

## Mapa drogowa

**1.1.0 (otagowane, bez GitHub Release):** Przejmij / współdzielone trwałe ACK w Checkmk, Przejęte przez, filtr PRZEJĘTE, wyszukiwanie, tłumienie powiadomień przy ACK, Otwórz w Checkmk, odwracalne lokalne Seen/Unseen. Komentarze CDN są jednoliniowe, bo Checkmk RAW 2.4 obcina wieloliniowe komentarze ACK.

**1.2.0 (Windows, wydane):** Skonsolidowany workflow zespołowy. Bezpieczne Zwolnij / Untake przejęć CDN (`POST /domain-types/acknowledge/actions/delete/invoke`), ciemne potwierdzenia Przejmij/Zwolnij, stany wiersza zamiast natywnych MessageBox, Checkmk jako źródło prawdy. Fazy 6A, 6B i 7A są COMPLETE / przetestowane na Windows. Zob. [docs/RELEASE_NOTES_1.2.0.md](docs/RELEASE_NOTES_1.2.0.md).

**1.3.0-beta.1 (macOS, pre-release):** Pierwszy publiczny build testerski macOS. Zob. [docs/RELEASE_NOTES_1.3.0-beta.1.md](docs/RELEASE_NOTES_1.3.0-beta.1.md).

**1.3.0 (FEATURE COMPLETE / FEATURE FREEZE):** Pierwsze wspólne wydanie Windows + macOS. Zachowanie Windows v1.2.0 zachowane; macOS menu-bar `.app` dla Intel x64 i Apple Silicon arm64. Zob. [docs/RELEASE_NOTES_1.3.0.md](docs/RELEASE_NOTES_1.3.0.md). W tym cyklu nie ma kolejnej fazy funkcji.

**Przyszłość / opcjonalnie:**

- Workflow zgłoszeń / integracja Zoho Desk

Ten projekt **nie** doda własnej współdzielonej bazy na potrzeby workflow zespołowego. Zgłoszenia pozostają pracą na później.

**Możliwe późniejsze usprawnienia**

- Nowoczesne toasty Windows, jeśli zmieni się model pakowania / tożsamości
- Dodatkowe formaty dźwięku powiadomień, jeśli pojawi się wyraźna potrzeba

## Licencja / atrybucja

- Licencja: [MIT](LICENSE) — Copyright © 2026 TimeWizard007
- Ikona aplikacji: oryginalny placeholder projektu (ciemny monitor + puls). **Nie** dołączono logo Checkmk
- Domyślny dźwięk: oryginalny / wygenerowany WAV w drzewie źródłowym
- NuGet: CommunityToolkit.Mvvm oraz Microsoft.Extensions.* (MIT)
- Instalator kompilowany Inno Setup 6 (kompilator nie jest w tym repozytorium)

Checkmk® jest znakiem towarowym Checkmk GmbH. Ten projekt jest niezależny i używa nazwy wyłącznie do opisu zgodności.
