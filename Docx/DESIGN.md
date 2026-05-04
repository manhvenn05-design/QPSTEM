---
name: QPSTEM Brand Identity
colors:
  surface: '#fafaf4'
  surface-dim: '#dadad5'
  surface-bright: '#fafaf4'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f4f4ee'
  surface-container: '#eeeee9'
  surface-container-high: '#e8e8e3'
  surface-container-highest: '#e3e3de'
  on-surface: '#1a1c19'
  on-surface-variant: '#42493d'
  inverse-surface: '#2f312e'
  inverse-on-surface: '#f1f1ec'
  outline: '#73796b'
  outline-variant: '#c2c9b9'
  surface-tint: '#3d6924'
  primary: '#3b6722'
  on-primary: '#ffffff'
  primary-container: '#538038'
  on-primary-container: '#f8ffed'
  inverse-primary: '#a2d582'
  secondary: '#41683b'
  on-secondary: '#ffffff'
  secondary-container: '#bfecb3'
  on-secondary-container: '#456c3f'
  tertiary: '#585d58'
  on-tertiary: '#ffffff'
  tertiary-container: '#707670'
  on-tertiary-container: '#f9fef7'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#bdf19c'
  primary-fixed-dim: '#a2d582'
  on-primary-fixed: '#082100'
  on-primary-fixed-variant: '#26500d'
  secondary-fixed: '#c2efb6'
  secondary-fixed-dim: '#a6d29b'
  on-secondary-fixed: '#002202'
  on-secondary-fixed-variant: '#2a4f25'
  tertiary-fixed: '#dfe4dd'
  tertiary-fixed-dim: '#c3c8c1'
  on-tertiary-fixed: '#181d19'
  on-tertiary-fixed-variant: '#434843'
  background: '#fafaf4'
  on-background: '#1a1c19'
  surface-variant: '#e3e3de'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
    letterSpacing: -0.01em
  title-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  label-sm:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.01em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 12px
  md: 24px
  lg: 48px
  xl: 80px
  grid-columns: '12'
  gutter: 24px
---

## Brand & Style

The design system is anchored in a **Modern Corporate Minimalist** aesthetic, tailored specifically for the Vietnamese educational technology sector. It projects a personality of growth, precision, and accessibility. By utilizing a flat design approach, the interface prioritizes clarity of information and ease of navigation for students, parents, and educators.

The visual language avoids unnecessary ornamentation, focusing instead on high-quality white space and a systematic arrangement of elements. The emotional response is intended to be one of "Thanh lịch và Chuyên nghiệp" (Elegant and Professional), fostering a focused learning environment that feels both technologically advanced and naturally grounded.

## Colors

The palette for the design system is centered around a primary organic green (#6a994e), representing "Sự phát triển và Tri thức" (Growth and Knowledge). This is a strictly light-mode system to maintain a clean, "trắng sáng" (bright white) marketing feel.

- **Primary:** Used for key actions, progress indicators, and primary branding.
- **Secondary:** A deeper forest green for high-contrast text and interactive states.
- **Tertiary:** A very soft mint-grey tint used for subtle background sections and card fills.
- **Neutral:** A range of slate-greys (from #1a1c19 to #f8f9fa) to handle typography, borders, and UI foundations.

Functional colors for success, error, and warning should follow standard SaaS conventions but be slightly desaturated to match the primary brand tone.

## Typography

The design system utilizes the **Inter** font family exclusively to ensure a neutral, utilitarian feel that performs exceptionally well in technical and educational contexts. 

To maintain a clean marketing aesthetic:
- **Max 2 Lines for Titles:** All headlines and card titles must be truncated or capped at two lines (line-clamp: 2) to ensure grid stability.
- **Vietnamese Readability:** Line heights are set slightly wider than default (minimum 1.5x for body text) to accommodate the stacked diacritics of Tiếng Việt without visual crowding.
- **Hierarchy:** Contrast is created through font weight rather than size alone. Bold weights are reserved for structural headings, while Medium weights are used for interactive labels.

## Layout & Spacing

This design system adheres to a **Strict 8px Grid**. Every margin, padding, and height increment must be a multiple of 8px. 

- **Layout Model:** A 12-column fixed-width grid (max-width: 1200px) for desktop, transitioning to a fluid 4-column grid for mobile.
- **Rhythm:** Use `24px` (md) for standard component spacing and `48px` (lg) for vertical section breathing room.
- **Vertical Alignment:** Components like cards must utilize Flexbox or CSS Grid to maintain **equal heights** across a row, regardless of content length. Content inside cards should use a "space-between" or "auto-margin" strategy to ensure action buttons always align perfectly at the bottom of the card container.

## Elevation & Depth

The design system employs a **Low-contrast Outline** approach to depth. To maintain the "Flat" requirement while ensuring usability:

- **Borders over Shadows:** Depth is indicated by 1px solid borders in a light neutral shade (#e2e8f0) rather than heavy drop shadows.
- **Tonal Layers:** Interactive surfaces use subtle background color shifts (e.g., #f8f9fa to #f1f5f9) on hover.
- **Minimal Elevation:** If a shadow is absolutely required for a floating element (like a dropdown menu), it must be a "Soft Ambient Shadow": a 10% opacity grey with a large blur (20px) and no spread, making it feel like a natural part of the surface rather than a separate layer.

## Shapes

The shape language is **Soft and Professional**. Using a `roundedness: 1` setting ensures the UI feels modern and approachable without becoming "too playful" or "bubbly."

- **Standard Corners:** 4px (0.25rem) for small elements like checkboxes and tags.
- **Component Corners:** 8px (0.5rem) for buttons, input fields, and cards.
- **Icons:** Must be contained within square bounding boxes, using consistent stroke weights (1.5px or 2px) to match the Inter font's visual weight.

## Components

### Buttons
Primary buttons use the #6a994e fill with white text. Secondary buttons use a 1px border of the primary color with a transparent background. All buttons must have a height of 48px to maintain the 8px grid rhythm and provide a generous touch target.

### Cards
Cards are the primary container for educational content. They feature an 8px border-radius and a 1px neutral border. **Constraint:** Cards in the same row must always have equal heights. The "Call to Action" button (e.g., "Xem chi tiết" or "Đăng ký ngay") must be pinned to the bottom of the card.

### Input Fields
Inputs use a white background with a 1px light border. On focus, the border transitions to the primary green with a subtle 2px outer glow (ghost border). Labels are always positioned above the input field in `label-sm` typography.

### Chips & Tags
Used for STEM categories (e.g., "Toán học", "Lập trình"). These are flat, using the tertiary color (#f2f7f0) as a background and the primary color for text, with no border.

### Progress Bars
A critical element for QPSTEM. Use a thick 8px track (neutral-light) with a primary green fill to indicate course completion or lesson progress.