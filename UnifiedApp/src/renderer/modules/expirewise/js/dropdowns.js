/**
 * Simple Dropdown implementations for ExpireWise
 */

// Modern Dropdown - Simple autocomplete functionality
class ModernDropdown {
  constructor(input, options = {}) {
    this.input = input;
    this.options = options;
    this.items = options.items || [];
    this.onSelect = options.onSelect || (() => {});

    // Store reference for cleanup
    this.boundHandlers = {
      input: this.handleInput.bind(this),
      focus: this.handleFocus.bind(this),
      blur: this.handleBlur.bind(this)
    };

    this.init();
  }

  init() {
    // Add event listeners
    if (this.input) {
      this.input.addEventListener('input', this.boundHandlers.input);
      this.input.addEventListener('focus', this.boundHandlers.focus);
      this.input.addEventListener('blur', this.boundHandlers.blur);
    }
  }

  handleInput(e) {
    // Simple autocomplete - browser's datalist will handle this
  }

  handleFocus(e) {
    // Could show dropdown on focus
  }

  handleBlur(e) {
    // Hide dropdown on blur
  }

  updateItems(items) {
    this.items = items;
    // Update the datalist if it exists
    if (this.input) {
      // Try to get datalist using input.list property or by finding it via the list attribute
      let datalist = this.input.list;
      if (!datalist && this.input.getAttribute('list')) {
        datalist = document.getElementById(this.input.getAttribute('list'));
      }

      if (datalist) {
        datalist.innerHTML = '';
        items.forEach(item => {
          const option = document.createElement('option');
          option.value = typeof item === 'object' ? item.value : item;
          if (typeof item === 'object' && item.label) {
            option.textContent = item.label;
          }
          datalist.appendChild(option);
        });
      }
    }
  }

  updateOptions(newOptions) {
    // Update options - this method is called by the app
    if (newOptions.items) {
      this.updateItems(newOptions.items);
    }
    if (newOptions.onSelect) {
      this.onSelect = newOptions.onSelect;
    }
    if (newOptions.placeholder && this.input) {
      this.input.placeholder = newOptions.placeholder;
    }
  }

  getValue() {
    return this.input ? this.input.value : '';
  }

  setValue(value) {
    if (this.input) {
      this.input.value = value;
    }
  }

  clear() {
    if (this.input) {
      this.input.value = '';
    }
  }

  destroy() {
    if (this.input) {
      this.input.removeEventListener('input', this.boundHandlers.input);
      this.input.removeEventListener('focus', this.boundHandlers.focus);
      this.input.removeEventListener('blur', this.boundHandlers.blur);
    }
  }
}

// Custom Dropdown - Enhanced dropdown with additional features
class CustomDropdown {
  constructor(input, options = {}) {
    this.input = input;
    this.options = options;
    this.items = options.items || [];
    this.onSelect = options.onSelect || (() => {});
    this.placeholder = options.placeholder || '';

    this.boundHandlers = {
      input: this.handleInput.bind(this),
      change: this.handleChange.bind(this)
    };

    this.init();
  }

  init() {
    if (this.input) {
      this.input.addEventListener('input', this.boundHandlers.input);
      this.input.addEventListener('change', this.boundHandlers.change);

      if (this.placeholder) {
        this.input.placeholder = this.placeholder;
      }
    }
  }

  handleInput(e) {
    // Filter suggestions based on input
    const value = e.target.value.toLowerCase();
    if (this.options.onInput) {
      this.options.onInput(value);
    }
  }

  handleChange(e) {
    const value = e.target.value;
    this.onSelect(value);
  }

  updateItems(items) {
    this.items = items;
    // Update datalist
    if (this.input) {
      // Try to get datalist using input.list property or by finding it via the list attribute
      let datalist = this.input.list;
      if (!datalist && this.input.getAttribute('list')) {
        datalist = document.getElementById(this.input.getAttribute('list'));
      }

      if (datalist) {
        console.log('📝 Updating datalist for', this.input.id, ':', items.length, 'items');
        datalist.innerHTML = '';
        items.forEach(item => {
          const option = document.createElement('option');
          option.value = typeof item === 'object' ? item.value : item;
          if (typeof item === 'object' && item.label) {
            option.textContent = item.label;
          }
          datalist.appendChild(option);
        });
        console.log('✓ Datalist updated, now has', datalist.children.length, 'options');
      } else {
        console.warn('⚠️ Cannot update datalist for', this.input?.id, '- datalist not found');
      }
    }
  }

  updateOptions(newOptions) {
    // Update options - this method is called by the app
    if (newOptions.items) {
      this.updateItems(newOptions.items);
    }
    if (newOptions.onSelect) {
      this.onSelect = newOptions.onSelect;
    }
    if (newOptions.placeholder && this.input) {
      this.input.placeholder = newOptions.placeholder;
    }
    if (newOptions.onInput) {
      this.options.onInput = newOptions.onInput;
    }
  }

  getValue() {
    return this.input ? this.input.value : '';
  }

  setValue(value) {
    if (this.input) {
      this.input.value = value;
    }
  }

  clear() {
    if (this.input) {
      this.input.value = '';
    }
  }

  destroy() {
    if (this.input) {
      this.input.removeEventListener('input', this.boundHandlers.input);
      this.input.removeEventListener('change', this.boundHandlers.change);
    }
  }
}

// Make available globally
window.ModernDropdown = ModernDropdown;
window.CustomDropdown = CustomDropdown;
