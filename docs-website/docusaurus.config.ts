import type * as Preset from '@docusaurus/preset-classic';
import type {Config} from '@docusaurus/types';
import {themes as prismThemes} from 'prism-react-renderer';

const organizationName = 'PinataLabs';
const projectName = 'Typography';
const repoUrl = `https://github.com/${organizationName}/${projectName}`;

const config: Config = {
  title: 'PinataLabs.Typography',
  tagline: 'Load fonts, and lay out and measure text, from .NET',
  favicon: 'img/favicon.svg',

  // Published to GitHub Pages at https://pinatalabs.github.io/Typography/
  url: `https://${organizationName.toLowerCase()}.github.io`,
  baseUrl: `/${projectName}/`,
  organizationName,
  projectName,
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          // Docs-only mode: the docs are served from the site root.
          routeBasePath: '/',
          sidebarPath: './sidebars.ts',
          editUrl: `${repoUrl}/tree/main/docs-website/`,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        // Offline search: the index is built at compile time and served as
        // static assets, so it works on GitHub Pages with no search backend.
        hashed: true,
        // The site runs in docs-only mode (docs.routeBasePath === '/'), so the
        // indexer has to be pointed at the root too — it defaults to '/docs'
        // and would otherwise index nothing.
        docsRouteBasePath: '/',
        indexBlog: false,
        highlightSearchTermsOnTargetPage: true,
      },
    ],
  ],

  themeConfig: {
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Typography',
      logo: {
        alt: 'PinataLabs.Typography logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {to: '/api', label: 'API reference', position: 'left'},
        {
          href: repoUrl,
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Overview', to: '/'},
            {label: 'Installation', to: '/installation'},
            {label: 'Getting started', to: '/getting-started'},
            {label: 'API reference', to: '/api'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'GitHub', href: repoUrl},
            {label: 'Issues', href: `${repoUrl}/issues`},
            {label: 'Changelog', href: `${repoUrl}/blob/main/CHANGELOG.md`},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} PinataLabs. Licensed under the Apache License, Version 2.0.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'powershell', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
