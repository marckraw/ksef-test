# KsefWinFormsApp

Minimalny projekt WinForms (VS2022) do pobrania faktury z KSeF po numerze KSeF i wygenerowania PDF.

## Co zawiera UI

1. Pole `Numer KSeF faktury`.
2. Przycisk `Pobierz fakturę PDF`.
3. Dodatkowe pola wymagane do działania MVP: `NIP`, `Token KSeF`, `Base URL`, ścieżki generatora PDF.

## Uruchomienie

1. Otwórz `KsefWinForms.sln` w VS2022.
2. Przywróć NuGet packages.
3. Ustaw `KsefWinFormsApp` jako startup project.
4. Uzupełnij pola w UI:
   - NIP,
   - token KSeF,
   - numer KSeF faktury,
   - ścieżkę do skryptu CLI oficjalnego generatora PDF MF.
5. Kliknij `Pobierz fakturę PDF`.
