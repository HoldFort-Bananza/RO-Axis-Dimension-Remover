# RO Axis Dimension Remover

Samodzielny `.exe` dla Tekla Structures 2025, dla rysunków pojedynczej części
z profilem RO (rura okrągła). Dwa przyciski:

1. **Usuń wymiary do osi** — w jednym widoku wskazanym kliknięciem kasuje
   wymiary, które Tekla przy skośnym cięciu zaczepiła o teoretyczną **oś**
   rury zamiast o jej powierzchnię. Przycisk **naprawdę kasuje**
   (`dryRun: false`), Ctrl+Z w Tekli cofa.
2. **Wstaw wymiar wcięcia** — dla każdej skośnej ściany cięcia (≥ 10°)
   dorysowuje brakującą długość i szerokość wcięcia, a płaski wymiar
   promienia przy skosie zamienia na średnicę. Nie dubluje wymiarów, które
   już są.

Pełna historia reguły wykrywania, "brama bezpieczeństwa", przez którą musi
przejść każda jej zmiana, i znane ograniczenia są w `AGENTS.md` — to on jest
bazą wiedzy projektu, README to tylko skrót.

## Diagnostyka bez GUI

Automatyzacja nie klika w przycisk, więc `Program.cs` ma tryb konsolowy
(pełna lista przełączników w `AGENTS.md`), np.:

```
RoAxisDimensionRemover.exe --diag-active                # aktywny rysunek w Tekli
RoAxisDimensionRemover.exe --diag-mark "[Mark]"         # otwiera rysunek po Mark
RoAxisDimensionRemover.exe --diag-notch-fill-dryrun "[Mark]"  # co wstawiłby przycisk "Wstaw"
RoAxisDimensionRemover.exe --diag-dimension-style       # istniejące wymiary aktywnego rysunku
```

Wszystkie tryby `--diag-*` mają `dryRun: true` na sztywno, na zawsze — to
ścieżka bez człowieka przy przycisku i nigdy nie kasuje ani nie wstawia.
Log leci na `stdout`.

## Pliki

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | wykrywanie i kasowanie wymiarów do osi, zero UI |
| `NotchPilot.cs` | wstawianie wymiaru wcięcia i geometria ścian cięcia |
| `MainForm.cs` | UI: dwa przyciski, log do okna i do pliku |
| `Program.cs` | punkt wejścia, przełączniki `--diag-*` |
| `DiagRunner.cs` | diagnostyka bez GUI, tylko odczyt |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja |
| `TeklaWindowFocus.cs` | po realnej zmianie przełącza fokus Windows na okno Tekli (do Ctrl+Z) |
| `installer/` | instalator Inno Setup — nie dołącza bibliotek Tekla, dociąga je z NuGet po instalacji |

## Budowanie

```
dotnet build -c Debug -p:Platform=x64
ISCC.exe installer\setup.iss
```

`-p:Platform=x64` jest wymagane — inaczej ścieżka wyjścia nie zgadza się z
`installer/setup.iss`.
