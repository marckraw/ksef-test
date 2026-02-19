# KsefWinFormsApp

Minimalny projekt WinForms (.NET Framework 4.8, VS2019/VS2022) do pobrania faktury z KSeF po numerze KSeF i wygenerowania PDF.

## UI po zmianie

1. Ekran główny:
   - `Numer KSeF faktury`
   - `Wyjściowy plik PDF`
   - przycisk `Pobierz fakturę PDF`
   - przycisk `Ustawienia...`
   - panel `Log operacji (krok po kroku)` z przyciskiem `Wyczyść log`
2. Ekran `Ustawienia...`:
   - `KSeF Base URL`
   - `NIP`
   - `Token KSeF`
   - `Polecenie PDF` (np. `node`)
   - `Ścieżka wrappera PDF (.mjs/.js)`
   - `Szablon argumentów`
   - opcje `Dołącz kod QR` i `Dołącz metadane KSeF`

## Trwałość ustawień

Ustawienia zapisują się do lokalnego pliku JSON:

`%LOCALAPPDATA%\\KsefWinFormsApp\\settings.json`

## Uruchomienie

1. Otwórz `KsefWinForms.sln` w VS2019 lub VS2022.
2. Przywróć NuGet packages.
3. Ustaw `KsefWinFormsApp` jako startup project.
4. W aplikacji kliknij `Ustawienia...` i uzupełnij dane integracyjne.
5. Na ekranie głównym wpisz `Numer KSeF faktury` i wybierz plik wynikowy PDF.
6. Kliknij `Pobierz fakturę PDF`.

Podczas pobierania obserwuj log na dole:

- wpisy aplikacyjne (walidacja, start/koniec, wyjątki)
- wpisy HTTP (`HTTP ->` i `HTTP <-`) dla auth/sesji/pobierania faktury

## Generator PDF MF - co wpisać

1. `Polecenie PDF`: `node`
2. `Ścieżka wrappera PDF`: np. `C:\tools\ksef-pdf-generator\dist\index.js`
3. `Szablon argumentów`:

```text
{script} faktura {input} {output} {extra}
```

Szczegółowa instrukcja instalacji generatora:

- `docs/PDF_GENERATOR_SETUP_PL.md`
