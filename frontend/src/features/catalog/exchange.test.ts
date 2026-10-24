import { describe, expect, it } from 'vite-plus/test'
import type { CategoryDto } from '@/shared/api'
import { buildAiPrompt, EXAMPLE_ANSWER } from './exchange'

const t = (key: string, params?: Record<string, unknown>) => `${key}${params ? JSON.stringify(params) : ''}`
const category = { slug: 'tomaat', name: 'Tomaten' } as CategoryDto

describe('AI prompt', () => {
  it('contains the task, the existing categories and a parseable answer template', () => {
    const prompt = buildAiPrompt(t, 'Voeg bieten toe', [category])
    expect(prompt).toContain('Voeg bieten toe')
    expect(prompt).toContain('tomaat: Tomaten')
    const template = prompt.slice(prompt.indexOf('{'), prompt.indexOf('\n\ncatalog.exchange.prompt.rulesTitle'))
    expect(JSON.parse(template)).toHaveProperty('varieties')
  })

  it('falls back to the placeholder task', () => {
    expect(buildAiPrompt(t, '  ', [])).toContain('catalog.exchange.prompt.taskPlaceholder')
  })

  it('example answer has one row the API will reject (unknown category)', () => {
    expect(EXAMPLE_ANSWER.varieties?.map((v) => v.category)).toEqual(['biet', 'radijs'])
  })
})
