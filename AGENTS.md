# AGENTS.md — instrukcje dla dowolnego asystenta AI w tym repo

Ten plik jest tu specjalnie dla asystentów, które NIE czytają automatycznie
`CLAUDE.md` (np. GitHub Copilot, Qwen Coder, Cursor, Aider) — na wypadek gdy
praca przechodzi z Claude Code na inne narzędzie w połowie sesji. Jeśli
jesteś Claude Code: `CLAUDE.md` już się wczytał automatycznie, ten plik nie
wnosi nic nowego poza powtórzeniem tego samego w skrócie.

**Jeśli czytasz to jako inny asystent: przeczytaj `CLAUDE.md` w całości
zanim zmienisz jakikolwiek kod w tym repo.** To jest pełna baza wiedzy —
historia pięciu nieudanych/podejrzanych wersji reguły wykrywania, dokładny
stan bramy bezpieczeństwa, pułapki API. Ten plik to tylko skrót, żeby nic
krytycznego nie zginęło przy zmianie narzędzia.

## Najważniejsze zanim cokolwiek zrobisz

1. **To jest wtyczka do Tekla Structures 2025, która potrafi NAPRAWDĘ
   kasować dane w modelu.** Dwie wcześniejsze wersje reguły realnie
   skasowały dobre wymiary na żywym modelu. **Aktualna reguła (v4) MA
   ZNANY BŁĄD** - patrz "PUŁAPKA 5" niżej. Nie włączaj realnego kasowania.
2. **Sprawdź `dryRun` w `MainForm.cs` (`RunButton_Click`) WPROST W PLIKU,
   nie z tego opisu.** Stan na 2026-09-04 (późny wieczór): `dryRun: true`
   — program NIE kasuje, tylko loguje. Tak ma zostać, dopóki PUŁAPKA 5 nie
   będzie naprawiona i brama nie przejdzie od zera.
3. **NIE WALIDUJ REGUŁY PRZEZ JEJ WŁASNY DRY-RUN.** To najważniejsza
   lekcja z tego projektu i powód, dla którego błąd przeżył trzy "czyste"
   testy: dry-run i realne kasowanie używają tego samego kodu, więc zawsze
   się zgodzą - także gdy oba są błędne. Waliduj pytaniem "czy po tej
   operacji rysunek nadal opisuje wszystko, co musi opisywać?" I pytaj
   operatora BEZ podpowiadania odpowiedzi - "usuwa jeden z pary duplikatów,
   poprawnie?" to pytanie, które samo w sobie przemyca założenie.
3. **Nigdy nie zgaduj progu/reguły detekcji "na wyczucie".** Każda stała w
   `RoAxisDimensionService.cs` ma komentarz skąd się wzięła (zmierzona, nie
   zgadana). Jeśli trzeba zmienić regułę — zdobądź realne współrzędne z
   dry-run (`RoAxisDimensionRemover.exe --diag-active` albo
   `--diag-mark "[Mark]"`, zawsze bezpieczne, nigdy nie kasuje) zanim
   napiszesz kod.
4. **Każda zmiana reguły detekcji wymaga nowego przejścia bramy
   bezpieczeństwa od zera**, nawet jeśli poprzednia reguła była
   potwierdzona: dry-run → operator patrzy na żywy rysunek w Tekli w
   momencie kliknięcia → dopiero wtedy `dryRun: false`. Nie pytaj "czy mogę
   włączyć realne kasowanie" retorycznie - naprawdę czekaj na wyraźne "tak"
   od człowieka, konkretnie na TO pytanie, nie na ogólne "kontynuuj".

## PUŁAPKA 5 — znany błąd, otwarty, zdiagnozowany, do naprawy

**To jest jedyny priorytet w tym projekcie.**

Para `21`/`21` na `[3.5013]` to NIE duplikat, a dwa **PROSTOPADŁE** wymiary
tego samego skosu 45° - jeden mierzy offset poziomo (wzdłuż rury), drugi
pionowo (w poprzek). Oba pokazują `21` tylko dlatego, że kąt to dokładnie
45°. Reguła v4 patrzy na wartość i bliskość, uznaje je za duplikat, kasuje
jeden - i ginie cała jedna informacja (albo poziom, albo pion):

```
21,0  Start=(5775,0; 0,0;  0,0)   End=(5796,2; 21,2; 0,0)
21,0  Start=(5775,0; 0,0; 21,2)   End=(5796,2; 21,2; 0,0)
```

**Naprawa (nie zaimplementowana):** porównywać KIERUNEK POMIARU, nie tylko
wartość. Uwaga: wyświetlana wartość to RZUT rozpiętości `StartPoint`→
`EndPoint` na kierunek pomiaru, więc sama rozpiętość nie rozróżnia tych
dwóch (rzut na `(1,0,0)` i na `(0,1,0)` daje to samo 21,2). Trop z
refleksji nad `Tekla.Structures.Drawing.dll`: **istnieje `UpDirection`** -
dokończyć sprawdzanie gdzie dokładnie (typ/klasa), dopisać do logu
`[diag]`, zebrać dane z obu rysunków, DOPIERO potem pisać regułę.

**`[35270]` musi dalej działać:** tam para to `24` i `12` (RÓŻNE wartości,
plus są DWA wymiary `24`) - to prawdziwy duplikat, operator potwierdził
wizualnie. Fix nie może tego zepsuć.

Pełna historia i kroki: `CLAUDE.md`, "brama bezpieczeństwa" + "Następne
kroki" pkt 5.

## Szybkie fakty

- Stos: C#, .NET Framework 4.8, x64, Tekla Open API 2025.0.0 (NuGet).
- Build: `dotnet build RoAxisDimensionRemover.csproj -c Debug -p:Platform=x64`
  (flaga `-p:Platform=x64` KONIECZNA, inaczej ścieżka wyjścia się nie zgadza
  z instalatorem).
- Zamknij `RoAxisDimensionRemover.exe` (`taskkill /F /IM RoAxisDimensionRemover.exe`)
  przed przebudowaniem, jeśli działa - inaczej build się nie uda.
- Testowanie bez GUI: `RoAxisDimensionRemover.exe --diag-active` (aktywny
  rysunek w Tekli) - zawsze bezpieczne, `dryRun` na sztywno `true`.
- Branche: `dev` (domyślny, WIP) i `release` (ma trzymać potwierdzony kod -
  w praktyce oba branche zwykle mają identyczną treść po serii PR-ów w obie
  strony; sprawdzaj `git diff origin/dev origin/release`, nie zgaduj).
- Merge pull requestów na GitHubie robi człowiek (operator), nie asystent -
  API do merge jest tu świadomie nieużywane.
- Pliki `RoAxisDimensionService.cs`, `MainForm.cs`, `Program.cs`,
  `DiagRunner.cs`, `UpdateCheck.cs`, `TeklaWindowFocus.cs` - patrz
  `CLAUDE.md`, sekcja "Struktura plików", po opis każdego.

## Konwencje repo (patrz też `..\CLAUDE.md`, nadrzędny dla wszystkich
projektów Tekla w tym katalogu)

- Komentarze w kodzie i logi po polsku (czyta je operator). Komunikaty
  commitów po angielsku.
- Dane wyłącznie z Tekla Open API - nigdy zrzuty ekranu / odczyt pikseli.
