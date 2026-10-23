// Uses Safari's UI language; never reads cookies or starts a connection.
const t = key => browser.i18n.getMessage(key);
document.documentElement.lang = browser.i18n.getUILanguage();
for (const element of document.querySelectorAll('[data-i18n]')) {
 element.textContent = t(element.dataset.i18n);
}
