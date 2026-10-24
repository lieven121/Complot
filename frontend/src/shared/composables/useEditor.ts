import { computed, ref, type Ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useI18n } from 'vue-i18n'
import { toApiProblem } from '@/shared/api/problems'
import { useNotify } from './useNotify'
import { useServerErrors } from './useServerErrors'

interface Mutation<TVariables> {
  mutateAsync: (variables: TVariables) => Promise<unknown>
  isPending: Ref<boolean>
}

interface EditorOptions<TDto, TInput> {
  /** Form values for a new item (dto undefined) or an existing one. */
  toInput: (dto?: TDto) => TInput
  save: Mutation<{ id?: string; input: TInput }>
  remove?: Mutation<string>
  /** Display name used in the delete confirmation. */
  nameOf?: (dto: TDto) => string
}

/** Shared create/edit/delete flow for the catalog dialogs: one place for errors, toasts and confirmation. */
export function useEditor<TDto extends { id: string }, TInput>(options: EditorOptions<TDto, TInput>) {
  const { t } = useI18n()
  const confirm = useConfirm()
  const notify = useNotify()
  const errors = useServerErrors()

  const visible = ref(false)
  const editing = ref<TDto>()
  const form = ref(options.toInput()) as Ref<TInput>

  const open = (dto?: TDto) => {
    editing.value = dto
    form.value = options.toInput(dto)
    errors.clear()
    visible.value = true
  }

  const submit = async () => {
    try {
      await options.save.mutateAsync({ id: editing.value?.id, input: form.value })
      notify.success('common.saved')
      visible.value = false
    } catch (error) {
      const problem = toApiProblem(error)
      if (problem?.status === 400 && problem.errors) errors.set(problem)
      else notify.error(error)
    }
  }

  const remove = () => {
    const item = editing.value
    if (!item || !options.remove) return
    const remover = options.remove
    confirm.require({
      header: t('common.confirmDeleteTitle'),
      message: t('common.confirmDelete', { name: options.nameOf?.(item) ?? '' }),
      acceptProps: { label: t('common.delete'), severity: 'danger' },
      rejectProps: { label: t('common.cancel'), severity: 'secondary', outlined: true },
      accept: async () => {
        try {
          await remover.mutateAsync(item.id)
          notify.success('common.deleted')
          visible.value = false
        } catch (error) {
          notify.error(error)
        }
      },
    })
  }

  return {
    visible,
    editing,
    form,
    errors,
    open,
    submit,
    remove,
    isNew: computed(() => !editing.value),
    saving: computed(() => options.save.isPending.value),
  }
}
