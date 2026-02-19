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

Po buildzie skrypt CLI będzie zwykle dostępny jako:

- `...\ksef-pdf-generator\dist\index.js`

## 3. Argumenty CLI (ważne)

Generator działa na argumentach pozycyjnych:

```bash
node <script> <documentType> <inputXmlPath> <outputPdfPath> [optionalAdditionalDataJson]
```

W praktyce dla faktury:

- `documentType`: `faktura`
- `inputXmlPath`: ścieżka do XML
- `outputPdfPath`: ścieżka docelowego PDF
- `optionalAdditionalDataJson`: opcjonalny JSON (np. `nrKSeF`)

Przykład:

```bash
node dist/index.js faktura C:\tmp\invoice.xml C:\tmp\invoice.pdf "{\"nrKSeF\":\"ABC-123\"}"
```

## 4. Co wpisać w naszej aplikacji (Ustawienia...)

W `Ustawienia...` wpisz:

1. `Polecenie PDF (np. node)`: `node`
2. `Ścieżka wrappera PDF (.mjs/.js)`: pełna ścieżka do skryptu, np.
   `C:\tools\ksef-pdf-generator\dist\index.js`
3. `Szablon argumentów`:

```text
{script} faktura {input} {output} {extra}
```

## 5. Jak działa `{extra}` w tym projekcie

Main form automatycznie przekazuje `nrKSeF` jako JSON do `{extra}`.
Czyli nie trzeba wpisywać ręcznie JSON per faktura.

## 6. Najczęstsze problemy

1. `node` nie znaleziony:
   - dodaj Node do `PATH` lub wpisz pełną ścieżkę do `node.exe`.
2. Błędna ścieżka skryptu:
   - wskaż realny plik `.js`/`.mjs` po buildzie.
3. Błędne argumenty:
   - sprawdź, czy template jest pozycyjny, nie flagowy (`--input`, `--output`).

## 7. Źródła oficjalne

- https://github.com/CIRFMF/ksef-pdf-generator
- https://github.com/CIRFMF/ksef-client-csharp
