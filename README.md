# KSeF WinForms MVP

To repozytorium zawiera kompletną solucję VS2022:

1. `src/KsefIntegration` - moduł integracyjny KSeF (auth + pobranie XML + generowanie PDF).
2. `src/KsefWinFormsApp` - aplikację WinForms z ekranem głównym (numer KSeF + pobranie PDF) i osobnym widokiem ustawień.

## Start

1. Otwórz `KsefWinForms.sln` w Visual Studio 2022.
2. Przywróć paczki NuGet.
3. Ustaw `KsefWinFormsApp` jako Startup Project.
4. W aplikacji kliknij `Ustawienia...` i wpisz dane KSeF oraz generatora PDF.
5. Wróć do ekranu głównego, podaj numer KSeF i pobierz PDF.

## Dokumentacja

- `docs/INTEGRATION_GUIDE_PL.md`
- `docs/PDF_GENERATOR_SETUP_PL.md`
- `src/KsefWinFormsApp/README.md`
- `src/KsefIntegration/README.md`
