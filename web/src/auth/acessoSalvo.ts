// "Salvar o acesso" sem o sistema virar depósito de senha.
//
// A senha NUNCA é gravada por este código. Quem guarda senha é o gerenciador do
// navegador (cofre do sistema, cifrado, atrás da biometria) — aqui só pedimos a
// ele que guarde, pela Credential Management API, e lembramos do e-mail para o
// campo já vir preenchido.
//
// Guardar senha em localStorage seria entregá-la a qualquer XSS e a qualquer
// pessoa que abra o DevTools no balcão da loja. Pela mesma razão a §10 do
// CLAUDE.md já proíbe localStorage até para o token, que é bem menos sensível:
// token expira, senha o lojista reusa no banco.

const CHAVE_EMAIL = 'toolsdelivery.acesso.email'

export function lerEmailSalvo(): string {
  try {
    return localStorage.getItem(CHAVE_EMAIL) ?? ''
  } catch {
    // Navegador com dados de site bloqueados: o login funciona igual, só não
    // vem preenchido.
    return ''
  }
}

export function salvarEmail(email: string): void {
  try {
    localStorage.setItem(CHAVE_EMAIL, email)
  } catch {
    // Não salvar o e-mail não é motivo para o login falhar.
  }
}

export function esquecerEmail(): void {
  try {
    localStorage.removeItem(CHAVE_EMAIL)
  } catch {
    // Idem.
  }
}

interface CredencialDeSenha {
  new (dados: { id: string; password: string }): Credential
}

// Pede ao navegador que ofereça salvar a senha. Só Chrome e Edge implementam
// PasswordCredential; no Firefox, no Safari e no app Capacitor isto não existe e
// a função sai calada — nesses o prompt nativo do navegador, disparado pelos
// autocomplete do formulário, continua sendo o caminho.
export async function pedirParaNavegadorSalvar(email: string, senha: string): Promise<void> {
  const construtor = (window as unknown as { PasswordCredential?: CredencialDeSenha }).PasswordCredential

  if (!construtor || !navigator.credentials?.store) return

  try {
    await navigator.credentials.store(new construtor({ id: email, password: senha }))
  } catch {
    // Usuário recusou o prompt, ou a origem não é segura. Nada a fazer: é
    // escolha dele, e o login já aconteceu de qualquer forma.
  }
}
