# Type 1 test fonts

Real Type 1 font programs, and the reference data that `Type1ParityTests` compares them with.

## Fonts

Each `.pfb` file is an unmodified copy of the file at the given CTAN path, downloaded on 2026-11-22.

| File | Font | Source on CTAN | Licence | Why it is here |
| --- | --- | --- | --- | --- |
| `bchr8a.pfb` | Bitstream Charter Roman (`CharterBT-Roman`) | `fonts/charter/bchr8a.pfb` | Bitstream Charter licence, `LICENSE-Charter.txt` | StandardEncoding. 56 `seac` glyphs. CR line ends. |
| `cmr10.pfb` | Computer Modern Roman 10pt (`CMR10` 003.002) | `fonts/amsfonts/pfb/cmr10.pfb` | SIL Open Font License 1.1, `LICENSE-AMSFonts-OFL.txt` | Custom encoding array. 90 flex calls in 47 glyphs. Hint replacement. |
| `cmmi10.pfb` | Computer Modern Math Italic 10pt (`CMMI10` 003.002) | `fonts/amsfonts/pfb/cmmi10.pfb` | SIL Open Font License 1.1, `LICENSE-AMSFonts-OFL.txt` | Flex. `div` with a fractional side bearing. |
| `qtmr.pfb` | TeX Gyre Termes Regular 2.004 | `fonts/tex-gyre/type1/qtmr.pfb` | GUST Font License, `LICENSE-TeXGyre-GUST.txt` | URW base-35 derived (Nimbus Roman No9 L). 1090 glyphs, 831 subroutines. |

Licence notes:

- **Bitstream Charter.** `LICENSE-Charter.txt` is `fonts/charter/readme.charter` from CTAN. It grants
  permission to use, copy, modify and redistribute the fonts for any purpose, if the notice stays on
  all copies. BITSTREAM CHARTER is a registered trademark of Bitstream Inc.
- **AMS Computer Modern.** The font files state the licence in their header comments.
  `LICENSE-AMSFonts-OFL.txt` is `fonts/amsfonts/doc/OFL.txt` from CTAN. The font names are Reserved
  Font Names, so this folder holds no converted or modified copy of these fonts.
- **TeX Gyre Termes.** The font file states the licence in its header comments. The GUST Font License
  is the LaTeX Project Public License 1.3c with one added request. `qtmr.pfb` is one unmodified file of
  the TeX Gyre Termes distribution. The complete distribution, with its manifest and readme, is at
  <https://ctan.org/pkg/tex-gyre-termes>.

## Derived files

`tests/tools/type1/generate_fixtures.py` writes these files with fontTools. Do not edit them by hand.

| File | Contents |
| --- | --- |
| `<font>.json` | FontName, FontMatrix, built-in encoding, and the advance and outline of every glyph, from fontTools. |
| `bchr8a.pfa` | `bchr8a.pfb` as a PFA with a hexadecimal eexec section. |
| `bchr8a.fontfile` | `bchr8a.pfb` as the stream of a PDF `/FontFile`: cleartext, binary eexec section, trailer. |
| `containers.json` | The true section lengths of `bchr8a.fontfile`, and a wrong `/Length1` for the tests. |

The two converted fonts come from Bitstream Charter only, because its licence permits modified copies
under the same name.

To write the derived files again:

```
python tests/tools/type1/generate_fixtures.py
```
