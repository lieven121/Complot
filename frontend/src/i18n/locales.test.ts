import { describe, expect, it } from 'vite-plus/test'
import en from './locales/en.json'
import enRaw from './locales/en.json?raw'
import nlBE from './locales/nl-BE.json'
import nlBERaw from './locales/nl-BE.json?raw'

/**
 * JSON.parse silently keeps the last of two equal keys, so "category": "…" followed by "category": { … } would lose
 * a message. Walk the raw text and report keys that occur twice in the same object.
 */
function duplicateKeys(json: string): string[] {
  const duplicates: string[] = []
  const stack: (Set<string> | null)[] = []
  let expectKey = false
  for (let i = 0; i < json.length; i++) {
    const c = json[i]
    if (c === '{') {
      stack.push(new Set())
      expectKey = true
    } else if (c === '[') stack.push(null)
    else if (c === '}' || c === ']') stack.pop()
    else if (c === ',') expectKey = stack.at(-1) != null
    else if (c === '"') {
      let end = i + 1
      while (json[end] !== '"') end += json[end] === '\\' ? 2 : 1
      const key = json.slice(i + 1, end)
      const current = stack.at(-1)
      if (expectKey && current) {
        if (current.has(key)) duplicates.push(key)
        current.add(key)
      }
      expectKey = false
      i = end
    }
  }
  return duplicates
}

type Messages = { [key: string]: string | Messages }

const keys = (messages: Messages, prefix = ''): string[] =>
  Object.entries(messages).flatMap(([key, value]) => (typeof value === 'string' ? [`${prefix}${key}`] : keys(value, `${prefix}${key}.`)))

const lookup = (messages: Messages, key: string) =>
  key.split('.').reduce<string | Messages>((m, k) => (m as Messages)[k]!, messages) as string

const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1] ?? '').sort((a, b) => a.localeCompare(b))

describe('translations', () => {
  it('have no duplicate keys', () => {
    expect(duplicateKeys(nlBERaw)).toEqual([])
    expect(duplicateKeys(enRaw)).toEqual([])
    expect(duplicateKeys('{"a": "x", "a": {"b": "y"}}')).toEqual(['a'])
  })

  it('have the same keys in every language', () => {
    expect(new Set(keys(en))).toEqual(new Set(keys(nlBE)))
  })

  it('use the same placeholders in every language', () => {
    for (const key of keys(nlBE)) expect(placeholders(lookup(en, key)), key).toEqual(placeholders(lookup(nlBE, key)))
  })

  it('contain no characters that vue-i18n treats as syntax (plural pipe, linked messages)', () => {
    for (const messages of [nlBE, en]) for (const key of keys(messages)) expect(lookup(messages, key), key).not.toMatch(/[|@]/)
  })
})
