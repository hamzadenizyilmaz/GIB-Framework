import panel from '../assets/ivory/panel.svg';
import invoices from '../assets/ivory/invoices.svg';
import users from '../assets/ivory/users.svg';
import shield from '../assets/ivory/shield.svg';
import rules from '../assets/ivory/rules.svg';
import settings from '../assets/ivory/settings.svg';
import search from '../assets/ivory/search.svg';
import test from '../assets/ivory/test.svg';
import person from '../assets/ivory/person.svg';
import chevron from '../assets/ivory/chevron.svg';
import menu from '../assets/ivory/menu.svg';

const icons = {
  panel: [panel, 18, 18],
  invoices: [invoices, 18, 18],
  users: [users, 18, 18],
  shield: [shield, 18, 18],
  rules: [rules, 18, 18],
  settings: [settings, 18, 18],
  search: [search, 17, 17],
  test: [test, 16, 16],
  person: [person, 16, 16],
  chevron: [chevron, 12, 12],
  menu: [menu, 28, 10],
};

export function Icon({ name }) {
  const [src, width, height] = icons[name];
  return <img className="ivory-icon" src={src} width={width} height={height} alt="" aria-hidden="true" />;
}
