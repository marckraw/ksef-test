# Konfiguracja generatora PDF KSeF (MF)

Ten projekt jest przygotowany pod oficjalny generator PDF MF (`ksef-pdf-generator`) uruchamiany przez Node.js.

## 1. Instalacja Node.js

1. Zainstaluj Node.js LTS (zalecane 20+).
2. Sprawdź:

```bash
node -v
npm -v
```

## 2. Pobranie generatora MF

Możesz użyć bezpośrednio repozytorium generatora:

```bash
git clone https://github.com/CIRFMF/ksef-pdf-generator.git
cd ksef-pdf-generator
npm ci
npm run build
```

## 3. Ważne: moduł != CLI

Pliki z `dist` (np. `dist/ksef-fe-invoice-converter.mjs`, `dist/index.js`) to moduły biblioteki.
Uruchomienie ich bezpośrednio (`node dist/...`) zwykle nie wygeneruje PDF, mimo kodu wyjścia `0`.

Dlatego trzeba użyć **wrappera CLI**.

## 4. Wrapper CLI

1. Skopiuj plik `docs/ksef-pdf-cli-wrapper.mjs` z tego repo do katalogu generatora, np.:

`C:\tools\ksef-pdf-generator\ksef-pdf-cli-wrapper.mjs`

2. Wrapper importuje moduł:

`./dist/ksef-fe-invoice-converter.mjs`

czyli musi leżeć obok folderu `dist` generatora.

3. Test ręczny (CMD/PowerShell):

```bash
node C:\tools\ksef-pdf-generator\ksef-pdf-cli-wrapper.mjs faktura C:\tmp\invoice.xml C:\tmp\invoice.pdf "{\"nrKSeF\":\"ABC-123\"}"
```

Jeśli PDF się utworzy, konfiguracja jest poprawna.

## 5. Co wpisać w naszej aplikacji (Ustawienia...)

W `Ustawienia...` wpisz:

1. `Polecenie PDF (np. node)`: `node`
2. `Ścieżka wrappera PDF (.mjs/.js)`: pełna ścieżka do skryptu, np.
   `C:\tools\ksef-pdf-generator\ksef-pdf-cli-wrapper.mjs`
3. `Szablon argumentów`:

```text
{script} faktura {input} {output} {extra}
```

## 6. Jak działa `{extra}` w tym projekcie

Main form automatycznie przekazuje `nrKSeF` jako JSON do `{extra}`.
Czyli nie trzeba wpisywać ręcznie JSON per faktura.

## 7. Najczęstsze problemy

1. `node` nie znaleziony:
   - dodaj Node do `PATH` lub wpisz pełną ścieżkę do `node.exe`.
2. Błędna ścieżka skryptu:
   - wskaż wrapper CLI, nie moduł `dist/ksef-fe-invoice-converter.mjs`.
3. Błędne argumenty:
   - sprawdź, czy template jest pozycyjny, nie flagowy (`--input`, `--output`).

## 8. Źródła oficjalne

- https://github.com/CIRFMF/ksef-pdf-generator
- https://github.com/CIRFMF/ksef-client-csharp
