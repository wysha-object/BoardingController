import { useLocalization } from 'cs2/l10n'

export function useTranslate() {
  const { translate } = useLocalization()
  const t = (id: string, values: Record<string, string> = {}) => {
    let str = translate(id, id) ?? ''
    for (const key in values) {
      str = str.replace(`{{${key}}}`, values[key])
    }
    return str
  }
  return { t }
}
