# Matin Converter — applied desktop design system

The installed ui-ux-pro-max skill was read on 2026-11-03. Searches: design-system (Windows utility minimalist converter), refined design-system (productivity desktop minimal accessible), WPF virtualization/commands/resources, typography, focus/progress/errors, and neutral/blue palette.

The first category match was Calculator & Unit Converter and its neumorphic orange palette was unsuitable. The refined Flat Design style is applicable. Marketing hero/video layout recommendations are excluded. The user's explicit Fluent-inspired palette and two-section layout take precedence.

| Role | Light | Dark |
|---|---|---|
| Background | #F8FAFC | #111827 |
| Surface | #FFFFFF | #1F2937 |
| Text | #111827 | #F1F5F9 |
| Secondary text | #475569 | #BAC5D5 |
| Border | #E2E8F0 | #46566C |
| Accent | #2563EB | #60A5FA |
| Accent text | #FFFFFF | #10213A |
| Success | #15803D | #86EFAC |
| Error | #B91C1C | #FCA5A5 |

Segoe UI / Tahoma are local Windows fonts with Persian fallback. No verified Persian-specific font pairing was returned by the skill; the system-font choice follows the supplied RTL requirement and native Windows conventions. Base 14 DIP, supporting text 12 DIP, section 18 DIP, heading 28 DIP. Spacing uses an 8 DIP rhythm (4 DIP within compact controls). Cards have 12 DIP corners, controls 6–7 DIP. No gradients or decorative animation.

A compact side navigation contains only Convert and Settings. The conversion view dedicates its main area to a recycled, virtualized file list and a 300 DIP options column. The primary conversion action stays visible outside the options scroller. Advanced options start collapsed. Settings has three groups: conversion defaults, Explorer integration, and general preferences. Minimum window size is 900×650 DIP.

Use native WPF semantic controls, visible keyboard focus, accessible names for icon controls and inputs, clear disabled states and per-file statuses. Use dynamic theme resources; technical paths and extensions use LTR. Persian is RTL; English switches to LTR. Small vector path icons use consistent 1.5 DIP strokes; the existing application logo is embedded as a brand asset.

The progress window has a genuine completed-file progress measure, filename, counts, cancellation and a compact summary. Multi-selection transport must never show a separate window per input file. No fake percentages or hidden frame loss.

Reviewed real main/settings dark RTL windows; fixed inherited Window foreground/background and an over-stretched Clear action. WPF controls/list virtualization and frozen thumbnails follow the skill's WPF recommendations. Physical DPI/Windows 10 checks are tracked in TESTING.md.
