import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// The order here is the order a newcomer should read in: set up, load a font, measure text, then
// control the layout, then go further into scripts, features, rendering and the font data itself.
const sidebars: SidebarsConfig = {
  docsSidebar: [
    {
      type: 'category',
      label: 'Start here',
      collapsed: false,
      items: ['overview', 'installation', 'getting-started', 'concepts'],
    },
    {
      type: 'category',
      label: 'Fonts',
      collapsed: false,
      items: ['fonts/loading-fonts', 'fonts/system-fonts', 'fonts/font-descriptions', 'fonts/font-metrics'],
    },
    {
      type: 'category',
      label: 'Text layout',
      collapsed: false,
      items: [
        'text/measuring-text',
        'text/wrapping-and-alignment',
        'text/rich-text',
        'text/international-text',
        'text/fallback-fonts',
        'text/vertical-text',
        'text/opentype-features',
      ],
    },
    {
      type: 'category',
      label: 'Rendering',
      collapsed: false,
      items: ['rendering/glyph-renderers', 'rendering/color-fonts', 'rendering/hinting'],
    },
    {
      type: 'category',
      label: 'Unicode',
      collapsed: false,
      items: ['unicode/code-points-and-graphemes'],
    },
    {
      type: 'category',
      label: 'Reference',
      collapsed: false,
      items: [
        {
          type: 'category',
          label: 'API reference',
          link: {type: 'doc', id: 'api/index'},
          items: [
            'api/font-collections',
            'api/font',
            'api/text-options',
            'api/text-measurer',
            'api/text-renderer',
            'api/metrics',
            'api/geometry',
            'api/enumerations',
            'api/unicode',
            'api/exceptions',
          ],
        },
        'reference/platforms',
        'reference/migrating-from-sixlabors-fonts',
        'reference/troubleshooting',
        'reference/faq',
        'reference/licensing',
        'reference/contributing',
        {
          type: 'link',
          label: 'Changelog',
          href: 'https://github.com/PinataLabs/Typography/blob/main/CHANGELOG.md',
        },
      ],
    },
  ],
};

export default sidebars;
