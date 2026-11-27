# Glyph name data

The source data for `tests/tools/generate_glyph_names.py`, which writes `AdobeGlyphListData.Generated.cs` and
`PdfEncodingData.Generated.cs` in `src/PinataLabs.Typography.Unicode/Resources`. Run it from the repository root
with Python 3.9 or later, and no packages:

```
python tests/tools/generate_glyph_names.py
```

The output is deterministic. A run that changes a generated file means that a data file changed.

| File | What it is | Source |
| --- | --- | --- |
| `glyphlist.txt` | The Adobe Glyph List: 4,281 glyph names and their code points | [adobe-type-tools/agl-aglfn](https://github.com/adobe-type-tools/agl-aglfn) |
| `zapfdingbats.txt` | The ITC Zapf Dingbats glyph list: `a1` to `a206` | the same |
| `aglfn.txt` | The Adobe Glyph List for New Fonts: one preferred name for each of 586 code points | the same |
| `LICENSE-agl-aglfn.md` | The BSD 3-Clause License of those three files | the same |
| `Times-Roman.afm` | Its `C` codes are the Standard Encoding | the [Apache PDFBox](https://github.com/apache/pdfbox) copy of the Adobe core font metrics, unmodified |
| `Symbol.afm`, `ZapfDingbats.afm` | Their `C` codes are the built-in encodings of the two fonts | the same |
| `MustRead.html` | The Adobe permission paragraph for the three AFM files. Keep it next to them and do not modify it | the PDFBox copy, same directory |
| `pdf-latin-encodings.txt` | WinAnsi, MacRoman and MacExpert, as `code glyph-name` lines | pdf.js `src/core/encodings.js` at v6.5.438 |

NOTICE has the attribution and the license text of each file.

## How the encodings were checked

ISO 32000-1 Annex D has no machine-readable form, so each encoding comes from one source and agrees with the others:

- Standard, MacExpert and Symbol: the AFM or pdf.js table is the same, entry by entry, as the Apache PDFBox table.
- ZapfDingbats: the AFM has the codes 0x80 to 0x8D (`a89` to `a96`, `a205`, `a85`, `a206`, `a86` to `a88`) that PDFBox leaves out.
- WinAnsi: the pdf.js table applies the notes of Annex D: every unused code above 0x20 is `bullet`
  (0x7F, 0x81, 0x8D, 0x8F, 0x90, 0x9D, which Windows 1252 leaves unassigned), and 0xA0 and 0xAD are `space` and `hyphen`.
  PDFBox names those two `nbspace` and `sfthyphen`.
- MacRoman: 0xCA is `space`. The 15 entries that `pdf-latin-encodings.txt` marks `beyond Annex D` are Mac OS Roman
  symbols (`notequal`, `infinity`, `lessequal` and so on) that Annex D leaves out and PDFBox omits. pdf.js has them.

The generator also checks every code of WinAnsi against the Python `cp1252` codec and every code of MacRoman against
`mac_roman`, through the Adobe Glyph List, without normalization, and lists the codes where they differ on purpose.
The one name-level difference is MacRoman 0xBD, `Omega`: the Adobe Glyph List maps it to U+2126 OHM SIGN and the codec
has U+03A9.

Updating the data: replace the file, rerun the script, and review the diff of the generated files.
