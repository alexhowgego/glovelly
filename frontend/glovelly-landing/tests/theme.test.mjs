import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const css = await readFile(new URL('../src/styles/global.css', import.meta.url), 'utf8')
const light = css.match(/:root\s*\{([^}]*)\}/)?.[1]
const semantic = css.match(/\/\* Page colours[^]*?:root\s*\{([^}]*)\}/)?.[1]
const dark = css.match(/@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}/)?.[1]

function tokens(source) {
  assert.ok(source, 'theme token block exists')
  return Object.fromEntries([...source.matchAll(/(--[\w-]+)\s*:\s*(#[\da-f]{6})/gi)].map(([, key, value]) => [key, value]))
}

function contrast(a, b) {
  const luminance = (hex) => {
    assert.match(hex ?? '', /^#[\da-f]{6}$/i)
    const [r, g, b] = hex.slice(1).match(/../g).map((part) => {
      const channel = parseInt(part, 16) / 255
      return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
    })
    return 0.2126 * r + 0.7152 * g + 0.0722 * b
  }
  const values = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (values[0] + 0.05) / (values[1] + 0.05)
}

for (const [name, palette] of [
  ['light', { ...tokens(light), ...tokens(semantic) }],
  ['dark', { ...tokens(light), ...tokens(semantic), ...tokens(dark) }],
]) {
  test(`${name} page tokens maintain text and focus contrast`, () => {
    for (const key of ['--ink', '--blue', '--page-copy', '--page-subtle', '--page-aside']) {
      assert.ok(contrast(palette[key], palette['--paper']) >= 4.5, `${key} against page`)
    }
    assert.ok(contrast(palette['--action-text'], palette['--blue']) >= 4.5, 'button text')
    assert.ok(contrast(palette['--page-focus'], palette['--paper']) >= 3, 'focus on page')
    assert.ok(contrast('#f2b271', palette['--closing-surface']) >= 3, 'focus on closing section')
  })
}
