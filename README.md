# KSeF WinForms MVP

To repozytorium zawiera kompletną solucję VS2022:

1. `src/KsefIntegration` - moduł integracyjny KSeF (auth + pobranie XML + generowanie PDF).
2. `src/KsefWinFormsApp` - aplikację WinForms z polem numeru KSeF i przyciskiem pobierania PDF.

## Start

1. Otwórz `KsefWinForms.sln` w Visual Studio 2022.
2. Przywróć paczki NuGet.
3. Ustaw `KsefWinFormsApp` jako Startup Project.
4. W aplikacji uzupełnij: `NIP`, `Token KSeF`, `Numer KSeF`, ścieżkę do skryptu CLI generatora PDF MF.
5. Kliknij `Pobierz fakturę PDF`.

## Dokumentacja

- `docs/INTEGRATION_GUIDE_PL.md`
- `src/KsefWinFormsApp/README.md`
- `src/KsefIntegration/README.md`
