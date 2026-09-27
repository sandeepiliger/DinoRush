import '@fontsource/fredoka/latin-500.css';
import '@fontsource/fredoka/latin-600.css';
import '@fontsource/fredoka/latin-700.css';
import './ui/style.css';
import { Game } from './game';

const game = new Game();
// Dev builds expose the game for automated screenshot/playtest scripts.
if (import.meta.env.DEV) (window as unknown as { game: Game }).game = game;
game.installUnloadHook();
game.start().catch((err) => {
  console.error(err);
  const loading = document.getElementById('loading');
  if (loading) loading.querySelector('.loading-text')!.textContent = 'Something went wrong. Please restart.';
});
