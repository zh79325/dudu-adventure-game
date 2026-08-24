/**
 * CommandRouter - collects and dispatches command handlers from modules.
 */
export class CommandRouter {
  constructor() {
    /** @type {Object.<string, function(Object): *>} */
    this.handlers = {};
  }

  /**
   * Register a set of command handlers.
   * @param {Object.<string, function(Object): *>} commands
   */
  register(commands) {
    for (const key in commands) {
      if (Object.prototype.hasOwnProperty.call(commands, key)) {
        this.handlers[key] = commands[key];
      }
    }
  }

  /**
   * Execute a command by method name.
   * @param {string} method
   * @param {Object} params
   * @returns {*} result
   * @throws if method not found or handler throws
   */
  execute(method, params) {
    const handler = this.handlers[method];
    if (!handler) {
      throw new Error(`Method not found: ${method}`);
    }
    return handler(params || {});
  }

  /**
   * Get the list of all registered method names.
   * @returns {string[]}
   */
  getMethodList() {
    return Object.keys(this.handlers);
  }

  /**
   * Check if a method is registered.
   * @param {string} method
   * @returns {boolean}
   */
  hasMethod(method) {
    return method in this.handlers;
  }
}
