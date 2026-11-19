if (window.jQuery?.validator) {
    Object.assign(jQuery.validator.messages, {
        required: window.weddingText('Este campo é obrigatório.'),
        number: window.weddingText('Introduz um número válido.'),
        email: window.weddingText('Introduz um endereço de email válido.')
    });
}
