import { doc010 } from './manuals/doc010'
import { doc020 } from './manuals/doc020'
import { doc030 } from './manuals/doc030'
import { doc040 } from './manuals/doc040'
import { doc050 } from './manuals/doc050'
import { doc060 } from './manuals/doc060'
import { doc065 } from './manuals/doc065'
import { doc070 } from './manuals/doc070'
import { doc080 } from './manuals/doc080'
import { doc090 } from './manuals/doc090'

export type DocSection = {
  slug: string
  title: string
  summary: string
  content: string
}

export type DocManual = {
  slug: string
  title: string
  documentId: string
  revision: string
  status: string
  date?: string
  sections: DocSection[]
}

export const electronicsManuals: DocManual[] = [
  doc010,
  doc020,
  doc030,
  doc040,
  doc050,
  doc060,
  doc065,
  doc070,
  doc080,
  doc090,
]

export const firstElectronicsManual = electronicsManuals[0]
export const firstElectronicsSection = firstElectronicsManual.sections[0]
